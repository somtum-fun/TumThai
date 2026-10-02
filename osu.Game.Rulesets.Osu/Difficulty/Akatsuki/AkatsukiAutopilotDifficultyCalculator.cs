// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: ppy's 2024.1115.0 osu! difficulty algorithm, kept for Akatsuki Autopilot pp (akatsuki-pp-rs).
// The Autopilot changes only apply when OsuModAutopilot is in the mods; without it this is plain 2024.1115.0.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki
{
    public class AkatsukiAutopilotDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.0675;

        public override int Version => 20241007;

        public AkatsukiAutopilotDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new AkatsukiAutopilotDifficultyAttributes { Mods = mods };

            double clockRate = ModUtils.CalculateRateWithMods(mods);

            var aim = skills.OfType<AkatsukiAim>().Single(a => a.WithSliders);
            var aimWithoutSliders = skills.OfType<AkatsukiAim>().Single(a => !a.WithSliders);
            var speed = skills.OfType<AkatsukiSpeed>().Single();
            var flashlight = skills.OfType<AkatsukiFlashlight>().SingleOrDefault();

            double aimRating = Math.Sqrt(aim.DifficultyValue()) * difficulty_multiplier;
            double aimRatingNoSliders = Math.Sqrt(aimWithoutSliders.DifficultyValue()) * difficulty_multiplier;
            double speedRating = Math.Sqrt(speed.DifficultyValue()) * difficulty_multiplier;
            double speedNotes = speed.RelevantNoteCount();

            double flashlightRating = 0.0;

            if (flashlight != null)
                flashlightRating = Math.Sqrt(flashlight.DifficultyValue()) * difficulty_multiplier;

            double sliderFactor = aimRating > 0 ? aimRatingNoSliders / aimRating : 1;

            double aimDifficultyStrainCount = aim.CountTopWeightedStrains();
            double speedDifficultyStrainCount = speed.CountTopWeightedStrains();

            if (mods.Any(m => m is OsuModTouchDevice))
            {
                aimRating = Math.Pow(aimRating, 0.8);
                flashlightRating = Math.Pow(flashlightRating, 0.8);
            }

            if (mods.Any(h => h is OsuModRelax))
            {
                aimRating *= 0.9;
                speedRating = 0.0;
                flashlightRating *= 0.7;
            }

            // Akatsuki: Autopilot aims for the player, so aim is worth nothing and flashlight much less.
            if (mods.Any(h => h is OsuModAutopilot))
            {
                aimRating = 0.0;
                flashlightRating *= 0.4;
            }

            double baseAimPerformance = AkatsukiOsuStrainSkill.DifficultyToPerformance(aimRating);
            double baseSpeedPerformance = AkatsukiOsuStrainSkill.DifficultyToPerformance(speedRating);
            double baseFlashlightPerformance = 0.0;

            if (mods.Any(h => h is OsuModFlashlight))
                baseFlashlightPerformance = AkatsukiFlashlight.DifficultyToPerformance(flashlightRating);

            double basePerformance =
                Math.Pow(
                    Math.Pow(baseAimPerformance, 1.1) +
                    Math.Pow(baseSpeedPerformance, 1.1) +
                    Math.Pow(baseFlashlightPerformance, 1.1), 1.0 / 1.1
                );

            double starRating = basePerformance > 0.00001
                ? Math.Cbrt(AkatsukiAutopilotPerformanceCalculator.PERFORMANCE_BASE_MULTIPLIER) * 0.027 * (Math.Cbrt(100000 / Math.Pow(2, 1 / 1.1) * basePerformance) + 4)
                : 0;

            double preempt = IBeatmapDifficultyInfo.DifficultyRange(beatmap.Difficulty.ApproachRate, 1800, 1200, 450) / clockRate;
            double drainRate = beatmap.Difficulty.DrainRate;

            int hitCirclesCount = beatmap.HitObjects.Count(h => h is HitCircle);
            int sliderCount = beatmap.HitObjects.Count(h => h is Slider);
            int spinnerCount = beatmap.HitObjects.Count(h => h is Spinner);

            // osu!somtum: OsuHitWindows has since been changed to floor(...) - 0.5; this is the 2024.1115.0 great window.
            double hitWindowGreat = IBeatmapDifficultyInfo.DifficultyRange(beatmap.Difficulty.OverallDifficulty, 80, 50, 20) / clockRate;

            return new AkatsukiAutopilotDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,
                AimDifficulty = aimRating,
                SpeedDifficulty = speedRating,
                SpeedNoteCount = speedNotes,
                FlashlightDifficulty = flashlightRating,
                SliderFactor = sliderFactor,
                AimDifficultStrainCount = aimDifficultyStrainCount,
                SpeedDifficultStrainCount = speedDifficultyStrainCount,
                ApproachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5,
                OverallDifficulty = (80 - hitWindowGreat) / 6,
                DrainRate = drainRate,
                MaxCombo = beatmap.GetMaxCombo(),
                HitCircleCount = hitCirclesCount,
                SliderCount = sliderCount,
                SpinnerCount = spinnerCount,
            };
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();

            double clockRate = ModUtils.CalculateRateWithMods(mods);
            double overallDifficulty = beatmap.Difficulty.OverallDifficulty;
            double approachRate = beatmap.Difficulty.ApproachRate;

            // The first jump is formed by the first two hitobjects of the map.
            // If the map has less than two OsuHitObjects, the enumerator will not return anything.
            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                var lastLast = i > 1 ? beatmap.HitObjects[i - 2] : null;
                objects.Add(new AkatsukiOsuDifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], lastLast, clockRate, overallDifficulty, approachRate, objects, objects.Count));
            }

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            var skills = new List<Skill>
            {
                new AkatsukiAim(mods, true),
                new AkatsukiAim(mods, false),
                new AkatsukiSpeed(mods)
            };

            if (mods.Any(h => h is OsuModFlashlight))
                skills.Add(new AkatsukiFlashlight(mods));

            return skills.ToArray();
        }

        // For OsuDifficultyCalculator, which hands Relax/Autopilot plays to this one.
        internal IEnumerable<DifficultyHitObject> HitObjectsFor(IBeatmap beatmap, Mod[] mods) => CreateDifficultyHitObjects(beatmap, mods);

        internal Skill[] SkillsFor(IBeatmap beatmap, Mod[] mods) => CreateSkills(beatmap, mods);

        internal DifficultyAttributes AttributesFor(IBeatmap beatmap, Mod[] mods, Skill[] skills) => CreateDifficultyAttributes(beatmap, mods, skills);

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModTouchDevice(),
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
            new OsuModFlashlight(),
            new MultiMod(new OsuModFlashlight(), new OsuModHidden())
        };
    }
}
