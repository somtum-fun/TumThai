// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: ppy's 2024.1115.0 osu! difficulty algorithm, kept for Akatsuki Autopilot pp (akatsuki-pp-rs).

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Preprocessing;
using System.Linq;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class AkatsukiSpeed : AkatsukiOsuStrainSkill
    {
        private double skillMultiplier => 1.430;
        private double strainDecayBase => 0.3;

        private double currentStrain;
        private double currentRhythm;

        protected override int ReducedSectionCount => 5;

        // Akatsuki: Autopilot drops the distance part of the speed evaluator.
        private readonly bool hasAutopilotMod;

        public AkatsukiSpeed(Mod[] mods)
            : base(mods)
        {
            hasAutopilotMod = mods.Any(m => m is OsuModAutopilot);
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => (currentStrain * currentRhythm) * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(((AkatsukiOsuDifficultyHitObject)current).StrainTime);
            currentStrain += AkatsukiSpeedEvaluator.EvaluateDifficultyOf(current, hasAutopilotMod) * skillMultiplier;

            currentRhythm = AkatsukiRhythmEvaluator.EvaluateDifficultyOf(current);

            double totalStrain = currentStrain * currentRhythm;

            return totalStrain;
        }

        public double RelevantNoteCount()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            double maxStrain = ObjectDifficulties.Max();
            if (maxStrain == 0)
                return 0;

            return ObjectDifficulties.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxStrain * 12.0 - 6.0))));
        }
    }
}
