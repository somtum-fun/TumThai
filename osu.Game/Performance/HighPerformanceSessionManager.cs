// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime;
using System.Threading;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Logging;

namespace osu.Game.Performance
{
    public class HighPerformanceSessionManager : IHighPerformanceSessionManager
    {
        public bool IsSessionActive => activeSessions > 0;

        private const long low_ram_threshold_bytes = (long)(6.5 * 1024 * 1024 * 1024);

        private int activeSessions;

        private GCLatencyMode originalGCMode;
        private GCLatencyMode appliedGCMode;

        public IDisposable BeginSession()
        {
            enterSession();
            return new InvokeOnDisposal<HighPerformanceSessionManager>(this, static m => m.exitSession());
        }

        /// <summary>
        /// Invoked on the transition from zero to one active high-performance session so platform
        /// subclasses (Android ADPF / thread priority, Desktop MMCSS / power throttling) can engage
        /// hardware performance hints for gameplay.
        /// </summary>
        protected virtual void OnSessionStarted()
        {
        }

        /// <summary>
        /// Invoked when the last active high-performance session finishes so platform subclasses can
        /// release gameplay-specific OS scheduling and power boosts.
        /// </summary>
        protected virtual void OnSessionEnded()
        {
        }

        private void enterSession()
        {
            if (Interlocked.Increment(ref activeSessions) > 1)
            {
                Logger.Log($"High performance session requested ({activeSessions} running in total)");
                return;
            }

            Logger.Log("Starting high performance session");

            // Flush beatmap, skin, and sample loading allocations across all generations while still in PlayerLoader
            // (before gameplay starts) so the Gen 2 heap begins clean and avoids emergency mid-combo collections.
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);

            originalGCMode = GCSettings.LatencyMode;

            // On memory-tight mobile devices (e.g. <= 6 GB RAM), SustainedLowLatency suppresses blocking Gen 2 pauses
            // while still permitting concurrent background Gen 2 sweeps on marathon maps instead of hitting a sudden
            // stop-the-world OOM collection mid-song.
            long totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            bool useSustainedMode = RuntimeInfo.IsMobile && totalMemory > 0 && totalMemory < low_ram_threshold_bytes;

            appliedGCMode = useSustainedMode ? GCLatencyMode.SustainedLowLatency : GCLatencyMode.LowLatency;
            GCSettings.LatencyMode = appliedGCMode;

            try
            {
                OnSessionStarted();
            }
            catch (Exception ex)
            {
                Logger.Log($"Platform high-performance session hook failed on start: {ex.Message}", LoggingTarget.Performance);
            }
        }

        private void exitSession()
        {
            if (Interlocked.Decrement(ref activeSessions) > 0)
            {
                Logger.Log($"High performance session finished ({activeSessions} others remain)");
                return;
            }

            Logger.Log("Ending high performance session");

            try
            {
                OnSessionEnded();
            }
            catch (Exception ex)
            {
                Logger.Log($"Platform high-performance session hook failed on exit: {ex.Message}", LoggingTarget.Performance);
            }

            if (GCSettings.LatencyMode == appliedGCMode)
                GCSettings.LatencyMode = originalGCMode;

            // No GC.Collect() as we were already collecting at a higher frequency in the old mode.
        }
    }
}
