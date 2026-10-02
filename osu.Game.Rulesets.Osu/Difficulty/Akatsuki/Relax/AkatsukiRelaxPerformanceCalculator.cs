// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: Akatsuki's Relax pp (akatsuki-pp-rs osu_2019 @ 591de0d), ported 1:1.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Relax
{
    /// <summary>
    /// Akatsuki's Relax pp: a port of <c>osu_2019::pp::OsuPP</c>.
    /// Only the 300/100/50/miss counts and the max combo are used (slider ticks and ends are ignored).
    /// </summary>
    public class AkatsukiRelaxPerformanceCalculator : PerformanceCalculator
    {
        public AkatsukiRelaxPerformanceCalculator()
            : base(new OsuRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var attrs = (AkatsukiRelaxDifficultyAttributes)attributes;

            var state = new State(
                attrs,
                score.Mods,
                (uint)Math.Max(0, score.MaxCombo),
                (uint)score.Statistics.GetValueOrDefault(HitResult.Great),
                (uint)score.Statistics.GetValueOrDefault(HitResult.Ok),
                (uint)score.Statistics.GetValueOrDefault(HitResult.Meh),
                (uint)score.Statistics.GetValueOrDefault(HitResult.Miss));

            return state.Calculate();
        }

        private class State
        {
            private readonly AkatsukiRelaxDifficultyAttributes attributes;
            private readonly uint combo;
            private readonly uint nObjects;
            private readonly uint nMisses;
            private readonly uint n300;
            private readonly uint n100;
            private readonly uint n50;
            private readonly float acc;

            private readonly bool hd;
            private readonly bool fl;
            private readonly bool so;
            private readonly bool td;

            public State(AkatsukiRelaxDifficultyAttributes attributes, IReadOnlyList<Mod> mods, uint combo, uint n300, uint n100, uint n50, uint nMisses)
            {
                this.attributes = attributes;
                this.combo = combo;
                this.nMisses = nMisses;

                hd = mods.Any(m => m is OsuModHidden);
                fl = mods.Any(m => m is OsuModFlashlight);
                so = mods.Any(m => m is OsuModSpunOut);
                td = mods.Any(m => m is OsuModTouchDevice);

                nObjects = (uint)attributes.ObjectCount;

                // assert_hitresults (all of n300/n100/n50 are given)
                uint remaining = saturatingSub(saturatingSub(saturatingSub(saturatingSub(nObjects, n300), n100), n50), nMisses);

                if (remaining > 0)
                    n300 += remaining;

                this.n300 = n300;
                this.n100 = n100;
                this.n50 = n50;

                uint numerator = n50 + n100 * 2 + n300 * 6;
                acc = (float)numerator / nObjects / 6.0f;
            }

            public OsuPerformanceAttributes Calculate()
            {
                float totalHits = totalHitsCount();
                float multiplier = 1.09f;

                float effectiveMissCount = calculateEffectiveMissCount();

                // SO penalty
                if (so)
                    multiplier *= 1.0f - MathF.Pow(attributes.SpinnerCount / totalHits, 0.85f);

                float aimValue = computeAimValue(totalHits, effectiveMissCount);
                float speedValue = computeSpeedValue(totalHits, effectiveMissCount);
                float accValue = computeAccuracyValue(totalHits);

                float accDepression = 1.0f;

                double streamsNerf = Math.Round(attributes.AimStrain / attributes.SpeedStrain * 100.0, MidpointRounding.AwayFromZero) / 100.0;

                if (streamsNerf < 1.09)
                {
                    float accFactor = MathF.Abs(1.0f - acc);
                    accDepression = RustMath.Max(0.86f - accFactor, 0.5f);

                    if (accDepression > 0.0f)
                        aimValue *= accDepression;
                }

                float pp = MathF.Pow(
                               MathF.Pow(aimValue, 1.185f)
                               + MathF.Pow(speedValue, 0.83f * accDepression)
                               + MathF.Pow(accValue, 1.14f),
                               1.0f / 1.1f)
                           * multiplier;

                return new OsuPerformanceAttributes
                {
                    Aim = aimValue,
                    Speed = speedValue,
                    Accuracy = accValue,
                    Total = pp,
                    EffectiveMissCount = effectiveMissCount,
                };
            }

            private float computeAimValue(float totalHits, float effectiveMissCount)
            {
                // TD penalty
                float rawAim = td ? (float)Math.Pow(attributes.AimStrain, 0.8) : (float)attributes.AimStrain;

                float aimValue = RustMath.Powi(5.0f * RustMath.Max(rawAim / 0.0675f, 1.0f) - 4.0f, 3) / 100_000.0f;

                // Longer maps are worth more
                float lenBonus = lengthBonus(totalHits);
                aimValue *= lenBonus;

                // Penalize misses
                if (effectiveMissCount > 0.0f)
                    aimValue *= calculateMissPenalty(effectiveMissCount);

                // AR bonus
                double arFactor = attributes.ApproachRate > 10.33 ? 0.3 * (attributes.ApproachRate - 10.33) : 0.0;

                if (attributes.ApproachRate < 8.0)
                    arFactor = 0.025 * (8.0 - attributes.ApproachRate);

                aimValue *= 1.0f + (float)arFactor * lenBonus;

                // HD bonus
                if (hd)
                    aimValue *= 1.0f + 0.05f * (float)(11.0 - attributes.ApproachRate);

                // FL bonus
                if (fl)
                {
                    aimValue *= 1.0f
                                + 0.3f * RustMath.Min(totalHits / 200.0f, 1.0f)
                                + (totalHits > 200.0f ? 1 : 0)
                                * 0.25f
                                * RustMath.Min((totalHits - 200.0f) / 300.0f, 1.0f)
                                + (totalHits > 500.0f ? 1 : 0) * (totalHits - 500.0f) / 1600.0f;
                }

                // Scale with accuracy
                float od = (float)attributes.OverallDifficulty;
                aimValue *= 0.3f + acc / 2.0f;
                aimValue *= 0.98f + od * od / 2500.0f;

                return aimValue;
            }

            private float computeSpeedValue(float totalHits, float effectiveMissCount)
            {
                float speedValue = RustMath.Powi(5.0f * RustMath.Max((float)attributes.SpeedStrain / 0.0675f, 1.0f) - 4.0f, 3) / 100_000.0f;

                // Longer maps are worth more
                float lenBonus = lengthBonus(totalHits);
                speedValue *= lenBonus;

                // Penalize misses
                if (effectiveMissCount > 0.0f)
                    speedValue *= calculateMissPenalty(effectiveMissCount);

                // AR bonus
                if (attributes.ApproachRate > 10.33)
                {
                    double arFactor = attributes.ApproachRate > 10.33 ? 0.3 * (attributes.ApproachRate - 10.33) : 0.0;

                    if (attributes.ApproachRate < 8.0)
                        arFactor = 0.025 * (8.0 - attributes.ApproachRate);

                    speedValue *= 1.0f + (float)arFactor * lenBonus;
                }

                // HD bonus
                if (hd)
                    speedValue *= 1.0f + 0.05f * (float)(11.0 - attributes.ApproachRate);

                // Scaling the speed value with accuracy and OD
                float od = (float)attributes.OverallDifficulty;
                speedValue *= (0.93f + od * od / 750.0f)
                              * MathF.Pow(acc, (14.5f - (float)RustMath.Max(attributes.OverallDifficulty, 8.0)) / 2.0f);

                speedValue *= MathF.Pow(0.98f, n50 < totalHits / 500.0f ? 0.0f : n50 - totalHits / 500.0f);

                return speedValue;
            }

            private float computeAccuracyValue(float totalHits)
            {
                float nCircles = attributes.HitCircleCount;
                float n300F = n300;
                float n100F = n100;
                float n50F = n50;

                float betterAccPercentage = (nCircles > 0.0f ? 1 : 0)
                                            * RustMath.Max(((n300F - (totalHits - nCircles)) * 6.0f + n100F * 2.0f + n50F) / (nCircles * 6.0f), 0.0f);

                float accValue = MathF.Pow(1.52163f, (float)attributes.OverallDifficulty) * RustMath.Powi(betterAccPercentage, 24) * 2.83f;

                // Bonus for many hitcircles
                accValue *= RustMath.Min(MathF.Pow(nCircles / 1000.0f, 0.3f), 1.15f);

                // HD bonus
                if (hd)
                    accValue *= 1.08f;

                // FL bonus
                if (fl)
                    accValue *= 1.02f;

                return accValue;
            }

            private static float lengthBonus(float totalHits)
                => 0.88f
                   + 0.4f * RustMath.Min(totalHits / 2000.0f, 1.0f)
                   + (totalHits > 2000.0f ? 1 : 0) * 0.5f * MathF.Log10(totalHits / 2000.0f);

            private uint totalHitsCount() => Math.Min(n300 + n100 + n50 + nMisses, nObjects);

            private float calculateMissPenalty(float effectiveMissCount)
            {
                float totalHits = totalHitsCount();

                return 0.97f * MathF.Pow(1.0f - MathF.Pow(effectiveMissCount / totalHits, 0.5f), 1.0f + effectiveMissCount / 1.5f);
            }

            private float calculateEffectiveMissCount()
            {
                float comboBasedMissCount = 0.0f;

                float comboF = combo;
                float n100F = n100;
                float n50F = n50;

                if (attributes.SliderCount > 0)
                {
                    float fcThreshold = attributes.MaxCombo - 0.1f * attributes.SliderCount;
                    if (comboF < fcThreshold)
                        comboBasedMissCount = fcThreshold / RustMath.Max(comboF, 1.0f);
                }

                comboBasedMissCount = RustMath.Min(comboBasedMissCount, n100F + n50F + nMisses);
                return RustMath.Max(comboBasedMissCount, nMisses);
            }

            private static uint saturatingSub(uint a, uint b) => a > b ? a - b : 0;
        }
    }
}
