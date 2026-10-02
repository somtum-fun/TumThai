// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: Akatsuki's Relax pp (akatsuki-pp-rs osu_2019 @ 591de0d), ported 1:1.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Relax
{
    public enum AkatsukiRelaxSkillKind
    {
        Aim,
        Speed,
    }

    /// <summary>
    /// Port of <c>osu_2019::skill::Skill</c> and <c>osu_2019::skill_kind::SkillKind</c>, including the
    /// section handling of <c>osu_2019::stars::stars</c> (sections of 400ms × clock rate in unscaled map time).
    /// </summary>
    /// <remarks>
    /// <see cref="DifficultyValue"/> does not modify the state, so it can be read after every object for timed attributes,
    /// which matches the Rust calculator's <c>passed_objects</c>.
    /// </remarks>
    public class AkatsukiRelaxSkill : Skill
    {
        private const float speed_skill_multiplier = 1400.0f;
        private const float speed_strain_decay_base = 0.3f;

        private const float aim_skill_multiplier = 26.25f;
        private const float aim_strain_decay_base = 0.15f;

        private const float decay_weight = 0.9f;

        private const float single_spacing_threshold = 125.0f;
        private const float speed_angle_bonus_begin = 5.0f * 0.523598775598298873077107230546583814f; // 5 * FRAC_PI_6
        private const float pi_over_4 = MathF.PI / 4;
        private const float pi_over_2 = MathF.PI / 2;

        private const float min_speed_bonus = 75.0f;
        private const float max_speed_bonus = 45.0f;
        private const float speed_balancing_factor = 40.0f;

        private const float aim_angle_bonus_begin = 1.04719755119659774615421446109316763f; // FRAC_PI_3
        private const float timing_threshold = 107.0f;

        public readonly AkatsukiRelaxSkillKind Kind;

        private readonly float sectionLen;

        private float currentStrain = 1.0f;
        private float currentSectionPeak = 1.0f;
        private readonly List<float> strainPeaks = new List<float>();
        private float? prevTime;

        private float currentSectionEnd;
        private bool started;

        public AkatsukiRelaxSkill(Mod[] mods, AkatsukiRelaxSkillKind kind, float sectionLen)
            : base(mods)
        {
            Kind = kind;
            this.sectionLen = sectionLen;
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            var h = (AkatsukiRelaxDifficultyHitObject)current;

            if (!started)
            {
                // First object has no predecessor and thus no strain, handle distinctly.
                currentSectionEnd = MathF.Ceiling(h.Prev.Time / sectionLen) * sectionLen;

                // The second object is handled separately (no peaks are saved).
                while (h.Base.Time > currentSectionEnd)
                    currentSectionEnd += sectionLen;

                started = true;
            }
            else
            {
                while (h.Base.Time > currentSectionEnd)
                {
                    saveCurrentPeak();
                    startNewSectionFrom(currentSectionEnd);

                    currentSectionEnd += sectionLen;
                }
            }

            currentStrain *= strainDecay(h.Delta);
            currentStrain += strainValueOf(h) * skillMultiplier;

            currentSectionPeak = RustMath.Max(currentSectionPeak, currentStrain);
            prevTime = h.Base.Time;

            return currentStrain;
        }

        /// <summary>
        /// The weighted sum of the section peaks (the current section included), as <c>save_current_peak</c> + <c>difficulty_value</c>.
        /// </summary>
        public float DifficultyValueF32()
        {
            var peaks = new List<float>(strainPeaks) { currentSectionPeak };
            peaks.Sort((a, b) => b.CompareTo(a));

            float difficulty = 0.0f;
            float weight = 1.0f;

            foreach (float strain in peaks)
            {
                difficulty += strain * weight;
                weight *= decay_weight;
            }

            return difficulty;
        }

        public override double DifficultyValue() => DifficultyValueF32();

        private void saveCurrentPeak() => strainPeaks.Add(currentSectionPeak);

        private void startNewSectionFrom(float time) => currentSectionPeak = currentStrain * strainDecay(time - prevTime!.Value);

        private float skillMultiplier => Kind == AkatsukiRelaxSkillKind.Aim ? aim_skill_multiplier : speed_skill_multiplier;

        private float strainDecayBase => Kind == AkatsukiRelaxSkillKind.Aim ? aim_strain_decay_base : speed_strain_decay_base;

        private float strainDecay(float ms) => MathF.Pow(strainDecayBase, ms / 1000.0f);

        private float strainValueOf(AkatsukiRelaxDifficultyHitObject current)
        {
            switch (Kind)
            {
                case AkatsukiRelaxSkillKind.Aim:
                {
                    if (current.Base.IsSpinner)
                        return 0.0f;

                    float result = 0.0f;

                    if (current.PrevVals is (float prevJumpDist, float prevStrainTime))
                    {
                        if (current.Angle is float angle && angle > aim_angle_bonus_begin)
                        {
                            const float scale = 90.0f;

                            float angleBonus = MathF.Sqrt(
                                RustMath.Powi(MathF.Sin(angle - aim_angle_bonus_begin), 2)
                                * RustMath.Max(prevJumpDist - scale, 0.0f)
                                * RustMath.Max(current.JumpDist - scale, 0.0f));

                            result = 1.5f * applyDiminishingExp(RustMath.Max(angleBonus, 0.0f))
                                     / RustMath.Max(timing_threshold, prevStrainTime);
                        }
                    }

                    float jumpDistExp = applyDiminishingExp(current.JumpDist);
                    float travelDistExp = applyDiminishingExp(current.TravelDist);

                    float distExp = jumpDistExp + travelDistExp + MathF.Sqrt(travelDistExp * jumpDistExp);

                    return RustMath.Max(result + distExp / RustMath.Max(current.StrainTime, timing_threshold), distExp / current.StrainTime);
                }

                default:
                {
                    if (current.Base.IsSpinner)
                        return 0.0f;

                    float dist = RustMath.Min(single_spacing_threshold, current.TravelDist + current.JumpDist);
                    float deltaTime = RustMath.Max(max_speed_bonus, current.Delta);

                    float speedBonus = 1.0f;

                    if (deltaTime < min_speed_bonus)
                    {
                        float expBase = (min_speed_bonus - deltaTime) / speed_balancing_factor;
                        speedBonus += expBase * expBase;
                    }

                    float angleBonus = 1.0f;

                    if (current.Angle is float angle && angle < speed_angle_bonus_begin)
                    {
                        float expBase = MathF.Sin(1.5f * (speed_angle_bonus_begin - angle));
                        angleBonus = 1.0f + expBase * expBase / 3.57f;

                        if (angle < pi_over_2)
                        {
                            angleBonus = 1.28f;

                            if (dist < 90.0f && angle < pi_over_4)
                                angleBonus += (1.0f - angleBonus) * RustMath.Min((90.0f - dist) / 10.0f, 1.0f);
                            else if (dist < 90.0f)
                            {
                                angleBonus += (1.0f - angleBonus)
                                              * RustMath.Min((90.0f - dist) / 10.0f, 1.0f)
                                              * MathF.Sin((pi_over_2 - angle) / pi_over_4);
                            }
                        }
                    }

                    return (1.0f + (speedBonus - 1.0f) * 0.75f)
                           * angleBonus
                           * (0.95f + speedBonus * MathF.Pow(dist / single_spacing_threshold, 3.5f))
                           / current.StrainTime;
                }
            }
        }

        private static float applyDiminishingExp(float val) => MathF.Pow(val, 0.99f);
    }
}
