// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Scoring;

namespace osu.Game.Screens.Select
{
    // osu!somtum: pp helpers shared by the tooltip and the pp display on pp-sorted leaderboards.
    public partial class BeatmapLeaderboardScore
    {
        /// <summary>
        /// Returns the score's stored pp, or calculates it locally when there is none.
        /// Returns <see langword="null"/> if the beatmap or ruleset is not available for calculation.
        /// </summary>
        /// <param name="score">The score to get pp for.</param>
        /// <param name="difficultyCache">The difficulty cache used to get difficulty attributes.</param>
        /// <param name="cancellationToken">Cancels the calculation.</param>
        internal static async Task<double?> CalculatePerformanceAsync(ScoreInfo score, BeatmapDifficultyCache difficultyCache, CancellationToken cancellationToken = default)
        {
            if (score.PP.HasValue)
                return score.PP.Value;

            var attributes = await difficultyCache.GetDifficultyAsync(score.BeatmapInfo!, score.Ruleset, score.Mods, cancellationToken).ConfigureAwait(false);
            var performanceCalculator = score.Ruleset.CreateInstance().CreatePerformanceCalculator();

            // Performance calculation requires the beatmap and ruleset to be locally available.
            if (attributes?.DifficultyAttributes == null || performanceCalculator == null)
                return null;

            var result = await performanceCalculator.CalculateAsync(score, attributes.Value.DifficultyAttributes, cancellationToken).ConfigureAwait(false);
            return result.Total;
        }

        /// <summary>
        /// Formats pp as shown on the leaderboard, e.g. "1,234pp".
        /// </summary>
        /// <param name="pp">The pp value.</param>
        internal static LocalisableString FormatPerformance(double pp)
        {
            int ppValue = (int)Math.Round(pp, MidpointRounding.AwayFromZero);
            return LocalisableString.Interpolate(@$"{ppValue:N0}pp");
        }
    }
}
