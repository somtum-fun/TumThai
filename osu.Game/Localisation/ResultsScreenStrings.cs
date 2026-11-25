// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ResultsScreenStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ResultsScreen";

        /// <summary>
        /// "Achieved on {0:g}"
        /// </summary>
        public static LocalisableString AchievedOnDate(DateTimeOffset? date) => new TranslatableString(getKey(@"achieved_on_date"), @"Achieved on {0:g}", date);

        /// <summary>
        /// "Performance points are not granted for this score because the beatmap is not ranked."
        /// </summary>
        public static LocalisableString NoPPForUnrankedBeatmaps => new TranslatableString(getKey(@"no_pp_for_unranked_beatmaps"), @"Performance points are not granted for this score because the beatmap is not ranked.");

        /// <summary>
        /// "Performance points are not granted for this score because of unranked mods."
        /// </summary>
        public static LocalisableString NoPPForUnrankedMods => new TranslatableString(getKey(@"no_pp_for_unranked_mods"), @"Performance points are not granted for this score because of unranked mods.");

        /// <summary>
        /// "Performance points are not granted for failed scores."
        /// </summary>
        public static LocalisableString NoPPForFailedScores => new TranslatableString(getKey(@"no_pp_for_failed_scores"), @"Performance points are not granted for failed scores.");

        /// <summary>
        /// "Performance"
        /// </summary>
        public static LocalisableString PerformancePointsHeader => new TranslatableString(getKey(@"performance_points_header"), "Performance");

        /// <summary>
        /// "Personal best"
        /// </summary>
        public static LocalisableString PersonalBest => new TranslatableString(getKey(@"personal_best"), @"Personal best");

        /// <summary>
        /// "Accuracy:
        /// Error: {0:0.00}ms - {1:0.00}ms avg
        /// Unstable Rate: {2:0.00}"
        /// </summary>
        public static LocalisableString LegacyHealthGraphTooltipText(double earlyAverage, double lateAverage, double unstableRate) => new TranslatableString(getKey(@"legacy_health_graph_tooltip_text"), @"Accuracy:
Error: {0:0.00}ms - {1:0.00}ms avg
Unstable Rate: {2:0.00}", earlyAverage, lateAverage, unstableRate);

        /// <summary>
        /// "▼ Online Ranking ▼"
        /// </summary>
        public static LocalisableString LegacyOnlineRankingButtonText => new TranslatableString(getKey(@"legacy_online_ranking_button_text"), @"▼ Online Ranking ▼");

        /// <summary>
        /// "Ranking"
        /// </summary>
        public static LocalisableString RankingFooterButtonText => new TranslatableString(getKey(@"ranking_footer_button_text"), @"Ranking");

        /// <summary>
        /// "More statistics"
        /// </summary>
        public static LocalisableString MoreStatisticsFooterButtonText => new TranslatableString(getKey(@"more_statistics_footer_button_text"), @"More statistics");

        /// <summary>
        /// "Catalogue"
        /// </summary>
        public static LocalisableString CatalogueFooterButtonText => new TranslatableString(getKey(@"catalogue_footer_button_text"), @"Catalogue");

        /// <summary>
        /// "Watch replay"
        /// </summary>
        public static LocalisableString WatchReplayFooterButtonText => new TranslatableString(getKey(@"watch_replay_footer_button_text"), @"Watch replay");

        /// <summary>
        /// "Play again"
        /// </summary>
        public static LocalisableString PlayAgainFooterButtonText => new TranslatableString(getKey(@"play_again_footer_button_text"), @"Play again");

        /// <summary>
        /// "Multiplier"
        /// </summary>
        public static LocalisableString ModScoreMultiplierCaption => new TranslatableString(getKey(@"mod_score_multiplier_caption"), @"Multiplier");

        // TumThai: the V2 results screen (from torii, MIT).
        /// <summary>
        /// "Watch Replay"
        /// </summary>
        public static LocalisableString WatchReplay => new TranslatableString(getKey(@"watch_replay"), @"Watch Replay");

        /// <summary>
        /// "More info"
        /// </summary>
        public static LocalisableString MoreInfo => new TranslatableString(getKey(@"more_info"), @"More info");

        /// <summary>
        /// "Score overview"
        /// </summary>
        public static LocalisableString ScoreOverview => new TranslatableString(getKey(@"score_overview"), @"Score overview");

        /// <summary>
        /// "Play again"
        /// </summary>
        public static LocalisableString PlayAgain => new TranslatableString(getKey(@"play_again"), @"Play again");

        /// <summary>
        /// "Downloading replay…"
        /// </summary>
        public static LocalisableString DownloadingReplay => new TranslatableString(getKey(@"downloading_replay"), @"Downloading replay…");

        /// <summary>
        /// "Screenshot"
        /// </summary>
        public static LocalisableString Screenshot => new TranslatableString(getKey(@"screenshot"), @"Screenshot");

        /// <summary>
        /// "Replay unavailable"
        /// </summary>
        public static LocalisableString ReplayUnavailable => new TranslatableString(getKey(@"replay_unavailable"), @"Replay unavailable");

        /// <summary>
        /// "MODS"
        /// </summary>
        public static LocalisableString Mods => new TranslatableString(getKey(@"mods"), @"MODS");

        /// <summary>
        /// "by {0}"
        /// </summary>
        public static LocalisableString MappedBy(string mapper) => new TranslatableString(getKey(@"mapped_by"), @"by {0}", mapper);

        /// <summary>
        /// "FEATURED ARTIST"
        /// </summary>
        public static LocalisableString FeaturedArtist => new TranslatableString(getKey(@"featured_artist"), @"FEATURED ARTIST");

        /// <summary>
        /// "Combo"
        /// </summary>
        public static LocalisableString Combo => new TranslatableString(getKey(@"combo"), @"Combo");

        /// <summary>
        /// "Achieved on {0}"
        /// </summary>
        public static LocalisableString AchievedOn(string date) => new TranslatableString(getKey(@"achieved_on"), @"Achieved on {0}", date);

        /// <summary>
        /// "Leaderboard / More info"
        /// </summary>
        public static LocalisableString LeaderboardAndDetails => new TranslatableString(getKey(@"leaderboard_and_details"), @"Leaderboard / More info");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
