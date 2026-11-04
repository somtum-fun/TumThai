// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Scoring
{
    /// <summary>
    /// Hit windows matching <c>DroidHitWindow</c> and <c>PreciseDroidHitWindow</c> from osu!droid.
    /// </summary>
    public class OsuDroidHitWindows : OsuHitWindows
    {
        public static readonly DifficultyRange DROID_GREAT_WINDOW_RANGE = new DifficultyRange(100, 75, 50);
        public static readonly DifficultyRange DROID_OK_WINDOW_RANGE = new DifficultyRange(200, 150, 100);
        public static readonly DifficultyRange DROID_MEH_WINDOW_RANGE = new DifficultyRange(300, 250, 200);

        public static readonly DifficultyRange PRECISE_GREAT_WINDOW_RANGE = new DifficultyRange(85, 55, 25);
        public static readonly DifficultyRange PRECISE_OK_WINDOW_RANGE = new DifficultyRange(160, 120, 80);
        public static readonly DifficultyRange PRECISE_MEH_WINDOW_RANGE = new DifficultyRange(230, 180, 130);

        private readonly bool precise;

        private double great;
        private double ok;
        private double meh;

        public OsuDroidHitWindows(bool precise = false)
        {
            this.precise = precise;
        }

        public override void SetDifficulty(double difficulty)
        {
            if (precise)
            {
                great = 55 + 6 * (5 - difficulty);
                ok = 120 + 8 * (5 - difficulty);
                meh = 180 + 10 * (5 - difficulty);
            }
            else
            {
                great = 75 + 5 * (5 - difficulty);
                ok = 150 + 10 * (5 - difficulty);
                meh = 250 + 10 * (5 - difficulty);
            }
        }

        public override double WindowFor(HitResult result)
        {
            switch (result)
            {
                case HitResult.Great:
                    return great;

                case HitResult.Ok:
                    return ok;

                case HitResult.Meh:
                    return meh;

                case HitResult.Miss:
                    return MISS_WINDOW;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }
    }
}
