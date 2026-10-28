// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using ManagedBass;
using osu.Framework.Logging;
using osu.Framework.Threading;

namespace osu.Android.Performance
{
    /// <summary>
    /// Configures BASS's native Android AAudio backend (<see cref="Configuration.AndroidAAudio"/>)
    /// to match the device's hardware sample rate and native frame burst size so AudioFlinger routes
    /// output through the Fast Mixer / MMAP low-latency path without requiring a managed AAudio callback
    /// (which would trip Android 16+/One UI 9's <c>libaaudio.so</c> Cross-DSO CFI shadow check).
    /// Automatically relaxes buffers when Bluetooth audio sinks are active to prevent A2DP/BLE underruns.
    /// </summary>
    internal static class AndroidAudioOptimizer
    {
        // AOSP AudioDeviceInfo.TYPE_* constants (using integer values avoids API-level enum field warnings while
        // covering Bluetooth SCO/A2DP, HearingAid, and Android 12/13+ BLE Audio headsets/speakers/broadcasts).
        private const int type_wired_headset = 3;
        private const int type_wired_headphones = 4;
        private const int type_bluetooth_sco = 7;
        private const int type_bluetooth_a2dp = 8;
        private const int type_usb_device = 11;
        private const int type_usb_headset = 22;
        private const int type_hearing_aid = 23;
        private const int type_ble_headset = 26;
        private const int type_ble_speaker = 27;
        private const int type_ble_broadcast = 30;

        // BASS_DEVICE_REINIT (128) applies updated DevicePeriod / DeviceBufferLength to an already-open BASS device
        // without freeing loaded tracks, samples, or mixers.
        private const DeviceInitFlags bass_device_reinit = (DeviceInitFlags)128;

        private static AudioDeviceCallback? routeCallback;
        private static int lastLoggedPreInitPeriod;
        private static int lastLoggedPreInitBufferMs;
        private static int lastReinitializedDevice = -1;
        private static int lastReinitPeriod;
        private static int lastReinitBufferMs;
        private static bool? lastReinitNonStop;

        /// <summary>
        /// Reads persisted startup flags from <c>game.ini</c> (if present) and applies optimal BASS AAudio
        /// configuration before <c>SDLActivity.OnCreate</c> initializes the audio thread and calls <c>Bass.Init</c>.
        /// </summary>
        public static void ConfigureBeforeStartup(Context context)
        {
            readPersistedAudioFlags(context, out bool lowLatencyEnabled, out bool potatoMode);
            ApplyBassConfiguration(context, lowLatencyEnabled && !potatoMode, reinitActiveDevice: false);
        }

        private static int getHardwareSampleRate(Context context)
        {
            try
            {
                if (context.GetSystemService(Context.AudioService) is AudioManager audioManager)
                {
                    string? rateString = audioManager.GetProperty(AudioManager.PropertyOutputSampleRate);
                    if (int.TryParse(rateString, out int sampleRate) && sampleRate >= 8000 && sampleRate <= 192000)
                        return sampleRate;
                }
            }
            catch
            {
            }

            return 0;
        }

        /// <summary>
        /// Schedules <see cref="ApplyBassConfiguration"/> on the framework's <see cref="GameThread"/> (<c>AudioThread</c>)
        /// so <c>BASS_DEVICE_REINIT</c> safely updates the live BASS AAudio stream after <c>AudioManager.InitBass</c>
        /// (which otherwise hardcodes <c>Bass.DeviceBufferLength = 10</c> with a default 10 ms period).
        /// </summary>
        public static void ScheduleApplyOnAudioThread(Context context, GameThread? audioThread, bool requestLowLatency)
        {
            Context appContext = context.ApplicationContext ?? context;

            if (audioThread != null)
            {
                audioThread.Scheduler.Add(() => ApplyBassConfiguration(appContext, requestLowLatency, reinitActiveDevice: true));
            }
            else
            {
                ApplyBassConfiguration(appContext, requestLowLatency, reinitActiveDevice: false);
            }
        }

        /// <summary>
        /// Applies BASS AAudio buffer and device-period parameters tuned for the current output route and hardware tier.
        /// </summary>
        public static void ApplyBassConfiguration(Context context, bool requestLowLatency, bool reinitActiveDevice = false)
        {
            try
            {
                var audioManager = context.GetSystemService(Context.AudioService) as AudioManager;
                var packageManager = context.PackageManager;

                int sampleRate = getHardwareSampleRate(context);
                if (sampleRate <= 0)
                    sampleRate = 48000;

                int framesPerBurst = 0;

                if (audioManager != null)
                {
                    string? burstString = audioManager.GetProperty(AudioManager.PropertyOutputFramesPerBuffer);
                    if (int.TryParse(burstString, out int parsedBurst) && parsedBurst >= 32 && parsedBurst <= 4096)
                        framesPerBurst = parsedBurst;
                }

                bool hasProAudio = packageManager?.HasSystemFeature(PackageManager.FeatureAudioPro) == true;
                bool hasLowLatencyFeature = hasProAudio || packageManager?.HasSystemFeature(PackageManager.FeatureAudioLowLatency) == true;
                bool bluetoothOutputActive = isBluetoothOutputConnected(audioManager);

                // Always keep BASS's built-in native AAudio backend enabled on Android 8.1+.
                Bass.Configure(Configuration.AndroidAAudio, 1);
                Bass.Configure(Configuration.UpdatePeriod, 5);

                int devicePeriodConfig;
                int bufferMs;
                bool devNonStop;

                if (!requestLowLatency || bluetoothOutputActive)
                {
                    // Standard / Bluetooth-safe profile: avoids A2DP/SSC/LC3 codec packet underruns and saves battery.
                    devNonStop = false;
                    devicePeriodConfig = 10;
                    bufferMs = 40;
                }
                else
                {
                    // Low-latency profile: align BASS's device period to an exact integer multiple of the hardware burst
                    // (e.g. -192 frames = 4 ms @ 48 kHz) so AudioFlinger keeps the stream on the Fast Mixer / MMAP path.
                    int periodMs;

                    if (framesPerBurst > 0)
                    {
                        int targetMinFrames = Math.Max(64, (int)Math.Round(sampleRate * 0.0035)); // ~3.5 ms floor
                        int multiplier = Math.Max(1, (targetMinFrames + framesPerBurst - 1) / framesPerBurst);
                        int alignedFrames = Math.Clamp(framesPerBurst * multiplier, 64, 1024);

                        // Negative DevicePeriod tells BASS to use an exact frame count rather than milliseconds.
                        devicePeriodConfig = -alignedFrames;
                        periodMs = Math.Max(2, (int)Math.Ceiling(alignedFrames * 1000.0 / sampleRate));
                    }
                    else
                    {
                        devicePeriodConfig = 4;
                        periodMs = 4;
                    }

                    // BASS triggers AAudio PERFORMANCE_MODE_LOW_LATENCY when DeviceBufferLength < 50 ms.
                    // 3x hardware period (~12-16 ms) provides glitch-free hitsound mixing on Fast Mixer.
                    bufferMs = hasProAudio
                        ? Math.Clamp(periodMs * 3, 12, 16)
                        : hasLowLatencyFeature
                            ? Math.Clamp(periodMs * 3, 14, 18)
                            : 24;

                    // Keep the AAudio / USB-C DAC output stream continuously active so quiet sections in a map
                    // don't suspend the audio HAL and clip or delay the next hitsound.
                    devNonStop = true;
                }

                int previousBufferConfig = Bass.DeviceBufferLength;

                Bass.Configure(Configuration.DevNonStop, devNonStop);
                Bass.Configure(Configuration.DevicePeriod, devicePeriodConfig);
                Bass.Configure(Configuration.DeviceBufferLength, bufferMs);

                if (reinitActiveDevice)
                {
                    int currentDevice = Bass.CurrentDevice;

                    if (currentDevice > 0)
                    {
                        bool needsReinit = currentDevice != lastReinitializedDevice
                                           || lastReinitPeriod != devicePeriodConfig
                                           || lastReinitBufferMs != bufferMs
                                           || lastReinitNonStop != devNonStop
                                           || previousBufferConfig != bufferMs;

                        if (needsReinit)
                        {
                            bool reinitOk = Bass.Init(currentDevice, sampleRate, bass_device_reinit);
                            Bass.GetInfo(out BassInfo bassInfo);

                            // Clear any non-fatal BASS error state after reinit probing.
                            Bass.GetDeviceInfo(0, out _);

                            if (reinitOk)
                            {
                                lastReinitializedDevice = currentDevice;
                                lastReinitPeriod = devicePeriodConfig;
                                lastReinitBufferMs = bufferMs;
                                lastReinitNonStop = devNonStop;
                            }

                            Logger.Log($"[AndroidAudio] Reinitialised BASS device {currentDevice} (ok={reinitOk}, bt={bluetoothOutputActive}, "
                                       + $"rate={sampleRate} Hz, hwBurst={framesPerBurst}f, periodCfg={devicePeriodConfig}, targetBuffer={bufferMs} ms, "
                                       + $"latency={bassInfo.Latency} ms, minBuf={bassInfo.MinBufferLength} ms).",
                                LoggingTarget.Performance);
                        }

                        return;
                    }
                }

                if (lastLoggedPreInitPeriod != devicePeriodConfig || lastLoggedPreInitBufferMs != bufferMs)
                {
                    lastLoggedPreInitPeriod = devicePeriodConfig;
                    lastLoggedPreInitBufferMs = bufferMs;

                    Logger.Log($"[AndroidAudio] Configured BASS AAudio profile (bt={bluetoothOutputActive}, rate={sampleRate} Hz, "
                               + $"hwBurst={framesPerBurst}f, periodCfg={devicePeriodConfig}, targetBuffer={bufferMs} ms).",
                        LoggingTarget.Performance);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[AndroidAudio] Failed to apply BASS configuration: {ex.Message}", LoggingTarget.Performance);
            }
        }

        /// <summary>
        /// Monitors Android audio output route changes (e.g. connecting or disconnecting Bluetooth earbuds or USB-C DACs)
        /// so BASS buffer parameters automatically switch between low-latency wired/speaker and crackle-free Bluetooth profiles.
        /// </summary>
        public static void RegisterRouteMonitor(Context context, GameThread? audioThread, Func<bool> shouldUseLowLatency)
        {
            if (routeCallback != null)
                return;

            try
            {
                if (context.GetSystemService(Context.AudioService) is not AudioManager audioManager)
                    return;

                var handler = Looper.MainLooper != null ? new Handler(Looper.MainLooper) : null;
                routeCallback = new OutputRouteCallback(context.ApplicationContext ?? context, audioThread, shouldUseLowLatency);
                audioManager.RegisterAudioDeviceCallback(routeCallback, handler);
            }
            catch (Exception ex)
            {
                Logger.Log($"[AndroidAudio] Could not register audio route callback: {ex.Message}", LoggingTarget.Performance);
            }
        }

        private static bool isBluetoothOutputConnected(AudioManager? audioManager)
        {
            if (audioManager == null)
                return false;

            try
            {
                // Android 13+ (API 33): query AudioPolicyManager for the exact sink(s) actively routing game/music audio.
                // This avoids false positives from permanent built-in LE Audio Broadcast (Auracast, type 30) sinks or
                // connected smartwatches using HFP/SCO while game audio plays through built-in speakers or USB-C audio.
                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    using var builder = new AudioAttributes.Builder();
                    using var attributes = builder.SetUsage(AudioUsageKind.Game)?.SetContentType(AudioContentType.Music)?.Build();

                    if (attributes != null)
                    {
                        var activeDevices = audioManager.GetAudioDevicesForAttributes(attributes);

                        foreach (var device in activeDevices)
                        {
                            int type = (int)device.Type;

                            if (type == type_bluetooth_a2dp
                                || type == type_bluetooth_sco
                                || type == type_hearing_aid
                                || type == type_ble_headset
                                || type == type_ble_speaker
                                || type == type_ble_broadcast)
                            {
                                return true;
                            }
                        }

                        return false;
                    }
                }

                AudioDeviceInfo[]? devices = audioManager.GetDevices(GetDevicesTargets.Outputs);
                if (devices == null)
                    return false;

                bool hasWiredAudio = false;
                bool hasBluetoothMediaAudio = false;

                foreach (var device in devices)
                {
                    int type = (int)device.Type;

                    if (type == type_wired_headset
                        || type == type_wired_headphones
                        || type == type_usb_device
                        || type == type_usb_headset)
                    {
                        hasWiredAudio = true;
                    }
                    else if (type == type_bluetooth_a2dp
                             || type == type_hearing_aid
                             || type == type_ble_headset
                             || type == type_ble_speaker
                             || (type == type_bluetooth_sco && !OperatingSystem.IsAndroidVersionAtLeast(34) && audioManager.BluetoothScoOn))
                    {
                        hasBluetoothMediaAudio = true;
                    }
                }

                return hasBluetoothMediaAudio && !hasWiredAudio;
            }
            catch
            {
            }

            return false;
        }

        private static void readPersistedAudioFlags(Context context, out bool lowLatencyEnabled, out bool potatoMode)
        {
            lowLatencyEnabled = true;
            potatoMode = false;

            try
            {
                var externalDir = context.GetExternalFilesDir(string.Empty);
                if (externalDir == null)
                    return;

                string configPath = Path.Combine(externalDir.AbsolutePath, "game.ini");
                if (!File.Exists(configPath))
                    return;

                foreach (string rawLine in File.ReadLines(configPath))
                {
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (line.IsEmpty || line[0] == '#' || line[0] == '[')
                        continue;

                    int eqIndex = line.IndexOf('=');
                    if (eqIndex <= 0)
                        continue;

                    ReadOnlySpan<char> key = line.Slice(0, eqIndex).Trim();
                    ReadOnlySpan<char> val = line.Slice(eqIndex + 1).Trim();

                    if (key.Equals("AndroidLowLatencyAudio", StringComparison.OrdinalIgnoreCase))
                        lowLatencyEnabled = !val.Equals("False", StringComparison.OrdinalIgnoreCase);
                    else if (key.Equals("PotatoMode", StringComparison.OrdinalIgnoreCase))
                        potatoMode = val.Equals("True", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
            }
        }

        private sealed class OutputRouteCallback : AudioDeviceCallback
        {
            private readonly Context context;
            private readonly GameThread? audioThread;
            private readonly Func<bool> shouldUseLowLatency;

            public OutputRouteCallback(Context context, GameThread? audioThread, Func<bool> shouldUseLowLatency)
            {
                this.context = context;
                this.audioThread = audioThread;
                this.shouldUseLowLatency = shouldUseLowLatency;
            }

            public override void OnAudioDevicesAdded(AudioDeviceInfo[]? addedDevices)
            {
                base.OnAudioDevicesAdded(addedDevices);
                ScheduleApplyOnAudioThread(context, audioThread, shouldUseLowLatency());
            }

            public override void OnAudioDevicesRemoved(AudioDeviceInfo[]? removedDevices)
            {
                base.OnAudioDevicesRemoved(removedDevices);
                ScheduleApplyOnAudioThread(context, audioThread, shouldUseLowLatency());
            }
        }
    }
}
