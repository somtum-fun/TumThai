// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using Android.App;
using Android.Content;
using Android.OS;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Performance;
using ThreadPriority = global::Android.OS.ThreadPriority;

namespace osu.Android.Performance
{
    /// <summary>
    /// Android-specific high-performance session manager combining:
    /// <list type="bullet">
    /// <item>Linux thread scheduling priority elevation (<c>UrgentAudio</c> / <c>UrgentDisplay</c>) on critical framework threads.</item>
    /// <item>Android 12+ Dynamic Performance Framework (<see cref="PerformanceHintManager"/> / ADPF) hint sessions to pin gameplay threads to Prime/Big CPU cores and prevent DVFS frequency drops during sliders and streams.</item>
    /// </list>
    /// </summary>
    internal sealed class AndroidHighPerformanceSessionManager : HighPerformanceSessionManager, IDisposable
    {
        private const long min_target_duration_nanos = 4_000_000L;  // ~250 Hz ceiling
        private const long max_target_duration_nanos = 16_666_666L; // 60 Hz floor

        private readonly object sessionLock = new object();

        private GameHost? host;

        private volatile int drawTid;
        private volatile int updateTid;
        private volatile int inputTid;
        private volatile int audioTid;

        private float currentDisplayRefreshHz = 120f;
        private long currentTargetDurationNanos = 8_333_333L;
        private double lastReportedDrawTime = double.MinValue;
        private volatile bool activityPaused;

        private PerformanceHintManager.Session? hintSession;

        public void AttachHost(GameHost gameHost)
        {
            host = gameHost;

            // CoreCLR on Android leaves managed threads at default Linux nice (0).
            // Android allows unprivileged foreground apps to elevate their own threads via android.os.Process.setThreadPriority,
            // preventing background GC/network/database tasks from preempting audio mixing, touch input, or frame rendering.
            captureTidAndPriority(gameHost.AudioThread, ThreadPriority.UrgentAudio, tid => audioTid = tid);
            captureTidAndPriority(gameHost.InputThread, ThreadPriority.UrgentDisplay, tid => inputTid = tid);
            captureTidAndPriority(gameHost.DrawThread, ThreadPriority.UrgentDisplay, tid => drawTid = tid);
            captureTidAndPriority(gameHost.UpdateThread, ThreadPriority.Display, tid => updateTid = tid);
        }

        public void UpdateDisplayRefreshRate(float refreshHz)
        {
            if (refreshHz <= 0)
                return;

            currentDisplayRefreshHz = refreshHz;
            long targetNanos = computeTargetDurationNanos(refreshHz, IsSessionActive);
            Interlocked.Exchange(ref currentTargetDurationNanos, targetNanos);

            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
                return;

            lock (sessionLock)
            {
                try
                {
                    hintSession?.UpdateTargetWorkDuration(targetNanos);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Reports frame execution telemetry to Android's ADPF PowerHAL once per drawn frame while a gameplay session is active.
        /// </summary>
        public void ReportFrameIfNeeded()
        {
            if (!IsSessionActive || activityPaused || !OperatingSystem.IsAndroidVersionAtLeast(31))
                return;

            var drawThread = host?.DrawThread;
            if (drawThread == null)
                return;

            // Report at most once per rendered frame (60-240 Hz) rather than on every 1000-8000 Hz Update tick
            // so JNI overhead on the Update thread stays effectively zero.
            double drawTime = drawThread.Clock.CurrentTime;
            if (drawTime == lastReportedDrawTime)
                return;

            lastReportedDrawTime = drawTime;

            long targetNanos = Interlocked.Read(ref currentTargetDurationNanos);
            double drawMs = drawThread.Clock.ElapsedFrameTime;
            double updateMs = host?.UpdateThread?.Clock.ElapsedFrameTime ?? 0;
            double worstElapsedMs = Math.Max(drawMs, updateMs);

            // Keep reported work near the target budget during active gameplay so EAS uclamp_min maintains steady clocks
            // on Big/Prime cores instead of dropping frequencies between bursts, while reporting real spikes (> target) immediately.
            long measuredNanos = (long)(worstElapsedMs * 1_000_000.0);
            long minGameplayFloorNanos = (long)(targetNanos * 0.92);
            long actualNanos = Math.Clamp(Math.Max(measuredNanos, minGameplayFloorNanos), 100_000L, 100_000_000L);

            reportActualDurationApi31(actualNanos);
        }

        public void OnActivityPaused()
        {
            activityPaused = true;

            if (OperatingSystem.IsAndroidVersionAtLeast(31))
                disposeHintSessionApi31();
        }

        public void OnActivityResumed()
        {
            activityPaused = false;

            if (IsSessionActive && OperatingSystem.IsAndroidVersionAtLeast(31))
                ensureHintSessionApi31();
        }

        protected override void OnSessionStarted()
        {
            base.OnSessionStarted();

            // Boost UpdateThread from Display (-4) to UrgentDisplay (-8) during active gameplay.
            setThreadPriorityOnGameThread(host?.UpdateThread, ThreadPriority.UrgentDisplay);

            long targetNanos = computeTargetDurationNanos(currentDisplayRefreshHz, gameplayActive: true);
            Interlocked.Exchange(ref currentTargetDurationNanos, targetNanos);

            if (OperatingSystem.IsAndroidVersionAtLeast(31) && !activityPaused)
                ensureHintSessionApi31();
        }

        protected override void OnSessionEnded()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
                disposeHintSessionApi31();

            // Relax UpdateThread back to Display (-4) in menus/song-select to save power and heat.
            setThreadPriorityOnGameThread(host?.UpdateThread, ThreadPriority.Display);

            long targetNanos = computeTargetDurationNanos(currentDisplayRefreshHz, gameplayActive: false);
            Interlocked.Exchange(ref currentTargetDurationNanos, targetNanos);

            base.OnSessionEnded();
        }

        public void Dispose()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
                disposeHintSessionApi31();
        }

        private static long computeTargetDurationNanos(float refreshHz, bool gameplayActive)
        {
            double clampedHz = Math.Clamp(refreshHz, 60.0, 240.0);
            double frameNanos = 1_000_000_000.0 / clampedHz;

            if (gameplayActive)
                frameNanos *= 0.85; // 15% safety margin so ADPF ramps clocks before VSync deadlines are missed.

            return Math.Clamp((long)frameNanos, min_target_duration_nanos, max_target_duration_nanos);
        }

        private static void captureTidAndPriority(GameThread? gameThread, ThreadPriority priority, Action<int> storeTid)
        {
            gameThread?.Scheduler.Add(() =>
            {
                try
                {
                    int tid = Process.MyTid();
                    storeTid(tid);
                    Process.SetThreadPriority(priority);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[ADPF] Failed to set priority {priority} on {gameThread.Name}: {ex.Message}", LoggingTarget.Performance);
                }
            });
        }

        private static void setThreadPriorityOnGameThread(GameThread? gameThread, ThreadPriority priority)
        {
            gameThread?.Scheduler.Add(() =>
            {
                try
                {
                    Process.SetThreadPriority(priority);
                }
                catch
                {
                }
            });
        }

        [SupportedOSPlatform("android31.0")]
        private void ensureHintSessionApi31()
        {
            lock (sessionLock)
            {
                if (hintSession != null)
                {
                    try
                    {
                        hintSession.UpdateTargetWorkDuration(Interlocked.Read(ref currentTargetDurationNanos));
                        return;
                    }
                    catch
                    {
                        disposeHintSessionApi31();
                    }
                }

                try
                {
                    if (Application.Context.GetSystemService(Context.PerformanceHintService) is not PerformanceHintManager hintManager)
                        return;

                    var tids = collectThreadIds();
                    if (tids.Length == 0)
                        return;

                    long targetNanos = Interlocked.Read(ref currentTargetDurationNanos);
                    hintSession = hintManager.CreateHintSession(tids, targetNanos);

                    if (hintSession != null)
                        Logger.Log($"[ADPF] Created PerformanceHintManager session for {tids.Length} threads (target={targetNanos / 1_000_000.0:0.00} ms).", LoggingTarget.Performance);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[ADPF] Could not create PerformanceHintManager session: {ex.Message}", LoggingTarget.Performance);
                }
            }
        }

        [SupportedOSPlatform("android31.0")]
        private void reportActualDurationApi31(long actualNanos)
        {
            PerformanceHintManager.Session? session = hintSession;
            if (session == null)
            {
                // Thread IDs may finish registering one frame after session start; retry lazily if needed.
                if (drawTid > 0 || updateTid > 0)
                    ensureHintSessionApi31();

                return;
            }

            try
            {
                session.ReportActualWorkDuration(actualNanos);
            }
            catch
            {
            }
        }

        [SupportedOSPlatform("android31.0")]
        private void disposeHintSessionApi31()
        {
            lock (sessionLock)
            {
                if (hintSession == null)
                    return;

                try
                {
                    hintSession.Close();
                    hintSession.Dispose();
                }
                catch
                {
                }
                finally
                {
                    hintSession = null;
                }
            }
        }

        private int[] collectThreadIds()
        {
            var set = new HashSet<int>();

            if (drawTid > 0)
                set.Add(drawTid);
            if (updateTid > 0)
                set.Add(updateTid);
            if (inputTid > 0)
                set.Add(inputTid);
            if (audioTid > 0)
                set.Add(audioTid);

            if (set.Count == 0)
            {
                int currentTid = Process.MyTid();
                if (currentTid > 0)
                    set.Add(currentTid);
            }

            int[] result = new int[set.Count];
            set.CopyTo(result);
            return result;
        }
    }
}
