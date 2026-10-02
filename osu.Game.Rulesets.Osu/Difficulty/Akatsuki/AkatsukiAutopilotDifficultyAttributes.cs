// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: ppy's 2024.1115.0 osu! difficulty attributes, kept for Akatsuki Autopilot pp (akatsuki-pp-rs).
// The shared values live on OsuDifficultyAttributes so the existing UI keeps working; the ones the
// 2024.1115.0 performance calculator needs and the current attributes dropped are added here.

using Newtonsoft.Json;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki
{
    public class AkatsukiAutopilotDifficultyAttributes : OsuDifficultyAttributes
    {
        /// <summary>
        /// The perceived approach rate inclusive of rate-adjusting mods (DT/HT/etc).
        /// </summary>
        [JsonProperty("approach_rate")]
        public double ApproachRate { get; set; }

        /// <summary>
        /// The perceived overall difficulty inclusive of rate-adjusting mods (DT/HT/etc).
        /// </summary>
        [JsonProperty("overall_difficulty")]
        public double OverallDifficulty { get; set; }

        /// <summary>
        /// The beatmap's drain rate. This doesn't scale with rate-adjusting mods.
        /// </summary>
        [JsonProperty("drain_rate")]
        public double DrainRate { get; set; }
    }
}
