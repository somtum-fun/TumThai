// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: ppy's 2024.1115.0 osu! difficulty algorithm, kept for Akatsuki Autopilot pp (akatsuki-pp-rs).

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class AkatsukiAim : AkatsukiOsuStrainSkill
    {
        public AkatsukiAim(Mod[] mods, bool withSliders)
            : base(mods)
        {
            WithSliders = withSliders;
        }

        public readonly bool WithSliders;

        private double currentStrain;

        private double skillMultiplier => 25.18;
        private double strainDecayBase => 0.15;

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += AkatsukiAimEvaluator.EvaluateDifficultyOf(current, WithSliders) * skillMultiplier;

            return currentStrain;
        }
    }
}
