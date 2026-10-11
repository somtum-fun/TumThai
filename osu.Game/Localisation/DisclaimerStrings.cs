// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    // TumThai: the startup disclaimer's text.
    public static class DisclaimerStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.Disclaimer";

        /// <summary>
        /// "this is "
        /// </summary>
        public static LocalisableString TitlePart => new TranslatableString(getKey(@"title_part"), @"this is ");

        /// <summary>
        /// "a free and open-source rhythm game based on the "
        /// </summary>
        public static LocalisableString BasedOnParagraph => new TranslatableString(getKey(@"based_on_paragraph"), @"a free and open-source rhythm game based on the ");

        /// <summary>
        /// " codebase, originally developed by ppy Pty Ltd."
        /// </summary>
        public static LocalisableString OriginalCodeParagraph => new TranslatableString(getKey(@"original_code_paragraph"), @" codebase, originally developed by ppy Pty Ltd.");

        /// <summary>
        /// "this is a community project and is not affiliated with ppy Pty Ltd or osu! in any way."
        /// </summary>
        public static LocalisableString CommunityProjectParagraph => new TranslatableString(getKey(@"community_project_paragraph"),
            @"this is a community project and is not affiliated with ppy Pty Ltd or osu! in any way.");

        /// <summary>
        /// "found a problem? please report it on our GitHub:"
        /// </summary>
        public static LocalisableString ReportGitHubParagraph => new TranslatableString(getKey(@"report_github_paragraph"), @"found a problem? please report it on our GitHub:");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
