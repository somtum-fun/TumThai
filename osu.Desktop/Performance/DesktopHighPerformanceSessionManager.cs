// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Performance;

namespace osu.Desktop.Performance
{
    /// <summary>
    /// Desktop high-performance session manager that eliminates OS scheduler jitter, EcoQoS P/E-core mis-scheduling,
    /// and audio/input preemption during gameplay while remaining friendly to user-space tablet drivers (e.g. OpenTabletDriver).
    /// </summary>
    internal sealed class DesktopHighPerformanceSessionManager : HighPerformanceSessionManager, IDisposable
    {
        private const int process_power_throttling = 4;
        private const uint process_power_throttling_current_version = 1;
        private const uint process_power_throttling_execution_speed = 0x1;
        private const uint process_power_throttling_ignore_timer_resolution = 0x4;

        private const int avrt_priority_high = 1;

        private GameHost? host;
        private ProcessPriorityClass? savedProcessPriority;
        private bool timerPeriodActive;

        private IntPtr audioMmcssHandle;
        private IntPtr inputMmcssHandle;
        private IntPtr updateMmcssHandle;
        private IntPtr drawMmcssHandle;

        public void AttachHost(GameHost gameHost)
        {
            host = gameHost;

            if (OperatingSystem.IsWindows())
                disableWindowsPowerThrottling();

            // Keep Audio and Input slightly elevated even in menus so hitsound previews and cursor feel snappy.
            setManagedThreadPriority(gameHost.AudioThread, ThreadPriority.AboveNormal);
            setManagedThreadPriority(gameHost.InputThread, ThreadPriority.AboveNormal);
        }

        protected override void OnSessionStarted()
        {
            base.OnSessionStarted();

            if (OperatingSystem.IsWindows())
                enterWindowsGameplayMode();

            setManagedThreadPriority(host?.AudioThread, ThreadPriority.Highest);
            setManagedThreadPriority(host?.InputThread, ThreadPriority.Highest);
            setManagedThreadPriority(host?.DrawThread, ThreadPriority.AboveNormal);
            setManagedThreadPriority(host?.UpdateThread, ThreadPriority.AboveNormal);
        }

        protected override void OnSessionEnded()
        {
            setManagedThreadPriority(host?.UpdateThread, ThreadPriority.Normal);
            setManagedThreadPriority(host?.DrawThread, ThreadPriority.Normal);
            setManagedThreadPriority(host?.InputThread, ThreadPriority.AboveNormal);
            setManagedThreadPriority(host?.AudioThread, ThreadPriority.AboveNormal);

            if (OperatingSystem.IsWindows())
                exitWindowsGameplayMode();

            base.OnSessionEnded();
        }

        public void Dispose()
        {
            if (OperatingSystem.IsWindows() && timerPeriodActive)
            {
                try
                {
                    timeEndPeriod(1);
                }
                catch
                {
                }

                timerPeriodActive = false;
            }
        }

        private static void setManagedThreadPriority(GameThread? gameThread, ThreadPriority priority)
        {
            gameThread?.Scheduler.Add(() =>
            {
                try
                {
                    Thread.CurrentThread.Priority = priority;
                }
                catch
                {
                }
            });
        }

        [SupportedOSPlatform("windows")]
        private static void disableWindowsPowerThrottling()
        {
            try
            {
                var state = new ProcessPowerThrottlingState
                {
                    Version = process_power_throttling_current_version,
                    ControlMask = process_power_throttling_execution_speed | process_power_throttling_ignore_timer_resolution,
                    StateMask = 0,
                };

                using Process current = Process.GetCurrentProcess();
                setProcessInformation(current.Handle, process_power_throttling, ref state, (uint)Marshal.SizeOf<ProcessPowerThrottlingState>());
            }
            catch (Exception ex)
            {
                Logger.Log($"[DesktopPerf] Could not opt out of Windows power throttling: {ex.Message}", LoggingTarget.Performance);
            }
        }

        [SupportedOSPlatform("windows")]
        private void enterWindowsGameplayMode()
        {
            disableWindowsPowerThrottling();

            try
            {
                if (!timerPeriodActive && timeBeginPeriod(1) == 0)
                    timerPeriodActive = true;
            }
            catch
            {
            }

            try
            {
                using Process current = Process.GetCurrentProcess();
                ProcessPriorityClass existing = current.PriorityClass;

                // Use AboveNormal rather than High so background apps (browsers, overlays, indexers) never preempt us
                // while user-space tablet daemons such as OpenTabletDriver (running at Normal/AboveNormal) are never starved.
                if (existing == ProcessPriorityClass.Normal || existing == ProcessPriorityClass.BelowNormal || existing == ProcessPriorityClass.Idle)
                {
                    savedProcessPriority = existing;
                    current.PriorityClass = ProcessPriorityClass.AboveNormal;
                }
            }
            catch
            {
            }

            registerMmcssOnThread(host?.AudioThread, "Pro Audio", elevatePriority: true, h => audioMmcssHandle = h, () => audioMmcssHandle);
            registerMmcssOnThread(host?.InputThread, "Games", elevatePriority: true, h => inputMmcssHandle = h, () => inputMmcssHandle);
            registerMmcssOnThread(host?.DrawThread, "Games", elevatePriority: false, h => drawMmcssHandle = h, () => drawMmcssHandle);
            registerMmcssOnThread(host?.UpdateThread, "Games", elevatePriority: false, h => updateMmcssHandle = h, () => updateMmcssHandle);
        }

        [SupportedOSPlatform("windows")]
        private void exitWindowsGameplayMode()
        {
            revertMmcssOnThread(host?.UpdateThread, () => updateMmcssHandle, h => updateMmcssHandle = h);
            revertMmcssOnThread(host?.DrawThread, () => drawMmcssHandle, h => drawMmcssHandle = h);
            revertMmcssOnThread(host?.InputThread, () => inputMmcssHandle, h => inputMmcssHandle = h);
            revertMmcssOnThread(host?.AudioThread, () => audioMmcssHandle, h => audioMmcssHandle = h);

            if (savedProcessPriority.HasValue)
            {
                try
                {
                    using Process current = Process.GetCurrentProcess();
                    current.PriorityClass = savedProcessPriority.Value;
                }
                catch
                {
                }

                savedProcessPriority = null;
            }

            if (timerPeriodActive)
            {
                try
                {
                    timeEndPeriod(1);
                }
                catch
                {
                }

                timerPeriodActive = false;
            }
        }

        [SupportedOSPlatform("windows")]
        private static void registerMmcssOnThread(GameThread? gameThread, string taskName, bool elevatePriority, Action<IntPtr> setHandle, Func<IntPtr> getHandle)
        {
            gameThread?.Scheduler.Add(() =>
            {
                if (getHandle() != IntPtr.Zero)
                    return;

                try
                {
                    uint taskIndex = 0;
                    IntPtr handle = avSetMmThreadCharacteristicsW(taskName, ref taskIndex);

                    if (handle != IntPtr.Zero)
                    {
                        if (elevatePriority)
                            avSetMmThreadPriority(handle, avrt_priority_high);

                        setHandle(handle);
                    }
                }
                catch
                {
                }
            });
        }

        [SupportedOSPlatform("windows")]
        private static void revertMmcssOnThread(GameThread? gameThread, Func<IntPtr> getHandle, Action<IntPtr> setHandle)
        {
            gameThread?.Scheduler.Add(() =>
            {
                IntPtr handle = getHandle();
                if (handle == IntPtr.Zero)
                    return;

                setHandle(IntPtr.Zero);

                try
                {
                    avRevertMmThreadCharacteristics(handle);
                }
                catch
                {
                }
            });
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessPowerThrottlingState
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport(@"kernel32.dll", EntryPoint = @"SetProcessInformation", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool setProcessInformation(IntPtr hProcess, int processInformationClass, ref ProcessPowerThrottlingState processInformation, uint processInformationSize);

        [DllImport(@"winmm.dll", EntryPoint = @"timeBeginPeriod")]
        private static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport(@"winmm.dll", EntryPoint = @"timeEndPeriod")]
        private static extern uint timeEndPeriod(uint uPeriod);

        [DllImport(@"avrt.dll", EntryPoint = @"AvSetMmThreadCharacteristicsW", CharSet = CharSet.Unicode)]
        private static extern IntPtr avSetMmThreadCharacteristicsW(string taskName, ref uint taskIndex);

        [DllImport(@"avrt.dll", EntryPoint = @"AvSetMmThreadPriority")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool avSetMmThreadPriority(IntPtr avrtHandle, int priority);

        [DllImport(@"avrt.dll", EntryPoint = @"AvRevertMmThreadCharacteristics")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool avRevertMmThreadCharacteristics(IntPtr avrtHandle);
    }
}
