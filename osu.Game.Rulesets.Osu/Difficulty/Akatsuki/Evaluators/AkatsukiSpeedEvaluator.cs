// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: ppy's 2024.1115.0 osu! difficulty algorithm, kept for Akatsuki Autopilot pp (akatsuki-pp-rs).

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Evaluators
{
    public static class AkatsukiSpeedEvaluator
    {
        private const double single_spacing_threshold = 125; // 1.25 circles distance between centers
        private const double min_speed_bonus = 75; // ~200BPM
        private const double speed_balancing_factor = 40;
        private const double distance_multiplier = 0.94;

        /// <summary>
        /// Evaluates the difficulty of tapping the current object, based on:
        /// <list type="bullet">
        /// <item><description>time between pressing the previous and current object,</description></item>
        /// <item><description>distance between those objects,</description></item>
        /// <item><description>and how easily they can be cheesed.</description></item>
        /// </list>
        /// </summary>
        /// <param name="current">The object to evaluate.</param>
        /// <param name="hasAutopilotMod">Akatsuki: whether Autopilot is on, which removes the distance bonus.</param>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool hasAutopilotMod)
        {
            if (current.BaseObject is Spinner)
                return 0;

            // derive strainTime for calculation
            var osuCurrObj = (AkatsukiOsuDifficultyHitObject)current;
            var osuPrevObj = current.Index > 0 ? (AkatsukiOsuDifficultyHitObject)current.Previous(0) : null;

            double strainTime = osuCurrObj.StrainTime;
            double doubletapness = 1.0 - osuCurrObj.GetDoubletapness((AkatsukiOsuDifficultyHitObject?)osuCurrObj.Next(0));

            // Cap deltatime to the OD 300 hitwindow.
            // 0.93 is derived from making sure 260bpm OD8 streams aren't nerfed harshly, whilst 0.92 limits the effect of the cap.
            strainTime /= Math.Clamp((strainTime / osuCurrObj.GreatHitWindow) / 0.93, 0.92, 1);

            // speedBonus will be 0.0 for BPM < 200
            double speedBonus = 0.0;

            // Add additional scaling bonus for streams/bursts higher than 200bpm
            if (strainTime < min_speed_bonus)
                speedBonus = 0.75 * Math.Pow((min_speed_bonus - strainTime) / speed_balancing_factor, 2);

            double travelDistance = osuPrevObj?.TravelDistance ?? 0;
            // Akatsuki: under Autopilot the cursor moves itself, so travel distance gives no speed bonus.
            double distance = hasAutopilotMod ? 0 : travelDistance + osuCurrObj.MinimumJumpDistance;

            // Cap distance at single_spacing_threshold
            distance = Math.Min(distance, single_spacing_threshold);

            // Max distance bonus is 1 * `distance_multiplier` at single_spacing_threshold
            double distanceBonus = Math.Pow(distance / single_spacing_threshold, 3.95) * distance_multiplier;

            // Base difficulty with all bonuses
            double difficulty = (1 + speedBonus + distanceBonus) * 1000 / strainTime;

            // Apply penalty if there's doubletappable doubles
            return difficulty * doubletapness;
        }
    }
}
