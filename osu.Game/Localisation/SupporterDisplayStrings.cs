// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SupporterDisplayStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SupporterDisplay";

        /// <summary>
        /// "Eternal thanks to you for supporting somtum.fun!"
        /// </summary>
        // TumThai: new keys, so ppy's translations (which name osu!supporter) don't replace our text.
        public static LocalisableString ThankYouForSupporting => new TranslatableString(getKey(@"somtum_thank_you_for_supporting"), @"Eternal thanks to you for supporting somtum.fun!");

        /// <summary>
        /// "Consider getting a [Somtum Supporter Tag]({0}) to help keep somtum.fun running"
        /// </summary>
        public static LocalisableString ConsiderBecomingASupporter(string url) => new TranslatableString(getKey(@"somtum_consider_becoming_a_supporter"), @"Consider getting a [Somtum Supporter Tag]({0}) to help keep somtum.fun running", url);

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
