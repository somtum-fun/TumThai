// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: Akatsuki's Relax pp (akatsuki-pp-rs osu_2019 @ 591de0d), ported 1:1.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Relax
{
    /// <summary>
    /// Akatsuki's Relax star rating: a port of <c>osu_2019::stars::stars</c> (an early-2019 osu! algorithm).
    /// Slider paths are considered but stack leniency is ignored.
    /// </summary>
    public class AkatsukiRelaxDifficultyCalculator : DifficultyCalculator
    {
        private const float object_radius = 64.0f;
        private const float section_len = 400.0f;
        private const float difficulty_multiplier = 0.0675f;
        private const float normalized_radius = 52.0f;

        /// <summary>
        /// The objects of the current calculation, so that (timed) attributes can count combo without recomputing slider paths.
        /// </summary>
        private readonly Dictionary<HitObject, AkatsukiRelaxObject> objects = new Dictionary<HitObject, AkatsukiRelaxObject>(ReferenceEqualityComparer.Instance);

        public AkatsukiRelaxDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            double clockRate = ModUtils.CalculateRateWithMods(mods);

            var attributes = new AkatsukiRelaxDifficultyAttributes
            {
                Mods = mods,
                ApproachRate = adjustedApproachRate(beatmap.Difficulty.ApproachRate, clockRate),
                OverallDifficulty = adjustedOverallDifficulty(beatmap.Difficulty.OverallDifficulty, clockRate),
                CircleSize = beatmap.Difficulty.CircleSize,
                DrainRate = Math.Min(beatmap.Difficulty.DrainRate, 10.0f),
                ObjectCount = beatmap.HitObjects.Count,
            };

            if (beatmap.HitObjects.Count < 2)
                return attributes;

            var (radius, scalingFactor) = radiusAndScaling(beatmap.Difficulty.CircleSize);
            bool hardRock = mods.Any(m => m is OsuModHardRock);

            foreach (var h in beatmap.HitObjects)
            {
                if (!objects.TryGetValue(h, out var obj))
                    obj = new AkatsukiRelaxObject((OsuHitObject)h, beatmap, radius, scalingFactor, hardRock);

                attributes.MaxCombo += obj.Combo;

                switch (h)
                {
                    case Slider:
                        attributes.SliderCount++;
                        break;

                    case Spinner:
                        attributes.SpinnerCount++;
                        break;

                    default:
                        attributes.HitCircleCount++;
                        break;
                }
            }

            var aim = (AkatsukiRelaxSkill)skills[0];
            var speed = (AkatsukiRelaxSkill)skills[1];

            float aimStrain = MathF.Sqrt(aim.DifficultyValueF32()) * difficulty_multiplier;
            float speedStrain = MathF.Sqrt(speed.DifficultyValueF32()) * difficulty_multiplier;

            float stars = aimStrain + speedStrain + MathF.Abs(aimStrain - speedStrain) / 2.0f;

            attributes.StarRating = stars;
            attributes.AimStrain = aimStrain;
            attributes.SpeedStrain = speedStrain;

            return attributes;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            objects.Clear();

            double clockRate = ModUtils.CalculateRateWithMods(mods);
            var (radius, scalingFactor) = radiusAndScaling(beatmap.Difficulty.CircleSize);
            bool hardRock = mods.Any(m => m is OsuModHardRock);

            var baseObjects = new List<AkatsukiRelaxObject>(beatmap.HitObjects.Count);

            foreach (var h in beatmap.HitObjects)
            {
                var obj = new AkatsukiRelaxObject((OsuHitObject)h, beatmap, radius, scalingFactor, hardRock);
                objects[h] = obj;
                baseObjects.Add(obj);
            }

            var difficultyObjects = new List<DifficultyHitObject>();
            (float, float)? prevVals = null;

            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                var h = new AkatsukiRelaxDifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], clockRate, difficultyObjects, difficultyObjects.Count,
                    baseObjects[i], baseObjects[i - 1], prevVals, i >= 2 ? baseObjects[i - 2] : null, (float)clockRate, scalingFactor);

                difficultyObjects.Add(h);
                prevVals = (h.JumpDist, h.StrainTime);
            }

            return difficultyObjects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            float sectionLen = section_len * (float)ModUtils.CalculateRateWithMods(mods);

            return new Skill[]
            {
                new AkatsukiRelaxSkill(mods, AkatsukiRelaxSkillKind.Aim, sectionLen),
                new AkatsukiRelaxSkill(mods, AkatsukiRelaxSkillKind.Speed, sectionLen),
            };
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModTouchDevice(),
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
            new OsuModFlashlight(),
            new OsuModHidden(),
        };

        private static (float radius, float scalingFactor) radiusAndScaling(float cs)
        {
            float radius = object_radius * (1.0f - 0.7f * (cs - 5.0f) / 5.0f) / 2.0f;
            float scalingFactor = normalized_radius / radius;

            if (radius < 30.0f)
            {
                float smallCircleBonus = RustMath.Min(30.0f - radius, 5.0f) / 50.0f;
                scalingFactor *= 1.0f + smallCircleBonus;
            }

            return (radius, scalingFactor);
        }

        // Hit windows as in akatsuki-pp-rs' BeatmapAttributesBuilder (clock-rate adjusted, in f64).
        private static double adjustedApproachRate(float ar, double clockRate)
        {
            double preempt = difficultyRange(ar, 1800.0, 1200.0, 450.0) / clockRate;

            return preempt > 1200.0 ? (1800.0 - preempt) / 120.0 : (1200.0 - preempt) / 150.0 + 5.0;
        }

        private static double adjustedOverallDifficulty(float od, double clockRate)
        {
            double great = difficultyRange(od, 80.0, 50.0, 20.0) / clockRate;

            return (80.0 - great) / 6.0;
        }

        private static double difficultyRange(double difficulty, double min, double mid, double max)
        {
            if (difficulty > 5.0)
                return mid + (max - mid) * (difficulty - 5.0) / 5.0;
            if (difficulty < 5.0)
                return mid - (mid - min) * (5.0 - difficulty) / 5.0;

            return mid;
        }
    }
}
