// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using osu.Framework;
using osu.Framework.Logging;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Selectable rate (in Hz) for the input, audio, and multi-threaded update threads.
    /// </summary>
    public enum InputAudioHzMode
    {
        [Description("500 Hz (low-power / battery saver)")]
        Hz500 = 500,

        [Description("1000 Hz (standard)")]
        Hz1000 = 1000,

        [Description("2000 Hz (competitive)")]
        Hz2000 = 2000,

        [Description("4000 Hz (high-end polling)")]
        Hz4000 = 4000,

        [Description("8000 Hz (extreme 8K polling)")]
        Hz8000 = 8000,
    }

    public static class InputAudioHzDefaults
    {
        public static InputAudioHzMode ForThisMachine()
        {
            try
            {
                int cores = Environment.ProcessorCount;
                long totalMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                double totalGiB = totalMemoryBytes > 0 ? totalMemoryBytes / (1024.0 * 1024.0 * 1024.0) : 0;

                InputAudioHzMode chosen;

                if (RuntimeInfo.IsMobile)
                {
                    chosen = (cores <= 4 || (totalGiB > 0 && totalGiB < 3.5))
                        ? InputAudioHzMode.Hz500
                        : InputAudioHzMode.Hz1000;
                }
                else
                {
                    if (cores <= 2 || (totalGiB > 0 && totalGiB < 3.8))
                        chosen = InputAudioHzMode.Hz500;
                    else if (cores < 8 || (totalGiB > 0 && totalGiB < 7.5))
                        chosen = InputAudioHzMode.Hz1000;
                    else
                        chosen = InputAudioHzMode.Hz2000;
                }

                Logger.Log($"[AutoTune] Selected default input/audio rate {(int)chosen} Hz ({cores} logical cores, {totalGiB:0.0} GiB RAM).");
                return chosen;
            }
            catch
            {
                return InputAudioHzMode.Hz1000;
            }
        }
    }
}
