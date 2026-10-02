// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: ppy's 2024.1115.0 osu! difficulty algorithm, kept for Akatsuki Autopilot pp (akatsuki-pp-rs).

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Evaluators;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Skills
{
    /// <summary>
    /// Represents the skill required to memorise and hit every object in a map with the Flashlight mod enabled.
    /// </summary>
    public class AkatsukiFlashlight : StrainSkill
    {
        private readonly bool hasHiddenMod;

        public AkatsukiFlashlight(Mod[] mods)
            : base(mods)
        {
            hasHiddenMod = mods.Any(m => m is OsuModHidden);
        }

        private double skillMultiplier => 0.05512;
        private double strainDecayBase => 0.15;

        private double currentStrain;

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += AkatsukiFlashlightEvaluator.EvaluateDifficultyOf(current, hasHiddenMod) * skillMultiplier;

            return currentStrain;
        }

        public override double DifficultyValue() => GetCurrentStrainPeaks().Sum();

        public static double DifficultyToPerformance(double difficulty) => 25 * Math.Pow(difficulty, 2);
    }
}
