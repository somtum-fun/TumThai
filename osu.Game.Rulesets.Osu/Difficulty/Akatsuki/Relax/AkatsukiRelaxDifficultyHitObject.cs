// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: Akatsuki's Relax pp (akatsuki-pp-rs osu_2019 @ 591de0d), ported 1:1.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Relax
{
    /// <summary>
    /// Port of <c>osu_2019::difficulty_object::DifficultyObject</c>.
    /// </summary>
    public class AkatsukiRelaxDifficultyHitObject : DifficultyHitObject
    {
        internal readonly AkatsukiRelaxObject Base;
        internal readonly AkatsukiRelaxObject Prev;

        /// <summary>
        /// The previous difficulty object's (jump distance, strain time), if any.
        /// </summary>
        internal readonly (float JumpDist, float StrainTime)? PrevVals;

        internal readonly float JumpDist;
        internal readonly float TravelDist;
        internal readonly float? Angle;

        internal readonly float Delta;
        internal readonly float StrainTime;

        internal AkatsukiRelaxDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, int index,
                                                  AkatsukiRelaxObject @base, AkatsukiRelaxObject prev, (float, float)? prevVals, AkatsukiRelaxObject? prevPrev,
                                                  float clockRateF32, float scalingFactor)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            Base = @base;
            Prev = prev;
            PrevVals = prevVals;

            Delta = (@base.Time - prev.Time) / clockRateF32;
            StrainTime = RustMath.Max(Delta, 50.0f);

            Vector2 pos = @base.Pos;
            TravelDist = prev.TravelDist ?? 0.0f;
            Vector2 prevCursorPos = prev.EndPos;

            JumpDist = @base.IsSpinner ? 0.0f : RustMath.Length((pos - prevCursorPos) * scalingFactor);

            if (prevPrev != null)
            {
                Vector2 prevPrevCursorPos = prevPrev.EndPos;

                Vector2 v1 = prevPrevCursorPos - prev.Pos;
                Vector2 v2 = pos - prevCursorPos;

                float dot = RustMath.Dot(v1, v2);
                float det = v1.X * v2.Y - v1.Y * v2.X;

                Angle = MathF.Abs(MathF.Atan2(det, dot));
            }
        }
    }
}
