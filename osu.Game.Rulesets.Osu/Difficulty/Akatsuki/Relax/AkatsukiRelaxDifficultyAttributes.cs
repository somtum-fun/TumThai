// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: Akatsuki's Relax pp (akatsuki-pp-rs osu_2019 @ 591de0d), ported 1:1.

using Newtonsoft.Json;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Relax
{
    /// <summary>
    /// Port of <c>osu_2019::stars::OsuDifficultyAttributes</c>.
    /// </summary>
    public class AkatsukiRelaxDifficultyAttributes : DifficultyAttributes
    {
        [JsonProperty("aim_strain")]
        public double AimStrain { get; set; }

        [JsonProperty("speed_strain")]
        public double SpeedStrain { get; set; }

        /// <summary>
        /// The clock-rate adjusted approach rate.
        /// </summary>
        [JsonProperty("approach_rate")]
        public double ApproachRate { get; set; }

        /// <summary>
        /// The clock-rate adjusted overall difficulty.
        /// </summary>
        [JsonProperty("overall_difficulty")]
        public double OverallDifficulty { get; set; }

        [JsonProperty("circle_size")]
        public double CircleSize { get; set; }

        [JsonProperty("drain_rate")]
        public double DrainRate { get; set; }

        [JsonProperty("hit_circle_count")]
        public int HitCircleCount { get; set; }

        [JsonProperty("slider_count")]
        public int SliderCount { get; set; }

        [JsonProperty("spinner_count")]
        public int SpinnerCount { get; set; }

        /// <summary>
        /// The number of hit objects these attributes were calculated for
        /// (the Rust calculator's <c>passed_objects</c> for a partial calculation).
        /// </summary>
        [JsonProperty("object_count")]
        public int ObjectCount { get; set; }
    }
}
