// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ButtonSystemStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ButtonSystem";

        /// <summary>
        /// "solo"
        /// </summary>
        public static LocalisableString Solo => new TranslatableString(getKey(@"solo"), @"solo");

        /// <summary>
        /// "multi"
        /// </summary>
        public static LocalisableString Multi => new TranslatableString(getKey(@"multi"), @"multi");

        /// <summary>
        /// "playlists"
        /// </summary>
        public static LocalisableString Playlists => new TranslatableString(getKey(@"playlists"), @"playlists");

        /// <summary>
        /// "play"
        /// </summary>
        public static LocalisableString Play => new TranslatableString(getKey(@"play"), @"play");

        /// <summary>
        /// "edit"
        /// </summary>
        public static LocalisableString Edit => new TranslatableString(getKey(@"edit"), @"edit");

        /// <summary>
        /// "browse"
        /// </summary>
        public static LocalisableString Browse => new TranslatableString(getKey(@"browse"), @"browse");

        /// <summary>
        /// "settings"
        /// </summary>
        public static LocalisableString Settings => new TranslatableString(getKey(@"settings"), @"settings");

        /// <summary>
        /// "back"
        /// </summary>
        public static LocalisableString Back => new TranslatableString(getKey(@"back"), @"back");

        /// <summary>
        /// "exit"
        /// </summary>
        public static LocalisableString Exit => new TranslatableString(getKey(@"exit"), @"exit");

        /// <summary>
        /// "daily challenge"
        /// </summary>
        public static LocalisableString DailyChallenge => new TranslatableString(getKey(@"daily_challenge"), @"daily challenge");

        /// <summary>
        /// "lounge"
        /// </summary>
        public static LocalisableString Lounge => new TranslatableString(getKey(@"lounge"), @"lounge");

        /// <summary>
        /// "quick play"
        /// </summary>
        public static LocalisableString QuickPlay => new TranslatableString(getKey(@"quick_play"), @"quick play");

        /// <summary>
        /// "ranked play"
        /// </summary>
        public static LocalisableString RankedPlay => new TranslatableString(getKey(@"ranked_play"), @"ranked play");

        /// <summary>
        /// "Welcome to TumThai!Next on Mobile!"
        /// </summary>
        public static LocalisableString MobileDisclaimerHeader => new TranslatableString(getKey(@"tumthai_next_mobile_disclaimer_header"), @"Welcome to TumThai!Next on Mobile!");

        /// <summary>
        /// "You are running TumThai!Next, TumThai's performance stream: the same game, account, beatmaps and scores, on our modified framework (tumthai-framework). On Android you install TumThai or TumThai!Next from the download page.
        ///
        /// This mobile build includes low-latency AAudio output, high-refresh-rate display support, sustained performance mode, and configurable input/audio rates.
        ///
        /// If you run into any device-specific audio or graphics issues, you can adjust these options anytime in Settings!"
        /// </summary>
        public static LocalisableString MobileDisclaimerBody => new TranslatableString(getKey(@"tumthai_next_mobile_disclaimer_body"),
            @"You are running TumThai!Next, TumThai's performance stream: the same game, account, beatmaps and scores, on our modified framework (tumthai-framework). On Android you install TumThai or TumThai!Next from the download page.

This mobile build includes low-latency AAudio output, high-refresh-rate display support, sustained performance mode, and configurable input/audio rates.

If you run into any device-specific audio or graphics issues, you can adjust these options anytime in Settings!");

        /// <summary>
        /// "TumThai!Next isn't guaranteed to be stable"
        /// </summary>
        public static LocalisableString NextWarningHeader => new TranslatableString(getKey(@"tumthai_next_warning_header"), @"TumThai!Next isn't guaranteed to be stable");

        /// <summary>
        /// "TumThai!Next changes a lot in how the game renders, and some of it is still an experiment. You may see crashes, graphics glitches or slowdowns that TumThai doesn't have.
        ///
        /// If something goes wrong, go back to TumThai: Settings → General → Updates → Release stream, or pick TumThai in the Somtum Launcher. Your account, beatmaps and scores stay the same."
        /// </summary>
        public static LocalisableString NextWarningBody => new TranslatableString(getKey(@"tumthai_next_warning_body"),
            @"TumThai!Next changes a lot in how the game renders, and some of it is still an experiment. You may see crashes, graphics glitches or slowdowns that TumThai doesn't have.

If something goes wrong, go back to TumThai: Settings → General → Updates → Release stream, or pick TumThai in the Somtum Launcher. Your account, beatmaps and scores stay the same.");

        /// <summary>
        /// "I understand"
        /// </summary>
        public static LocalisableString NextWarningOkButton => new TranslatableString(getKey(@"tumthai_next_warning_ok_button"), @"I understand");

        /// <summary>
        /// "Go back to TumThai"
        /// </summary>
        public static LocalisableString NextWarningBackButton => new TranslatableString(getKey(@"tumthai_next_warning_back_button"), @"Go back to TumThai");

        /// <summary>
        /// "Got it!"
        /// </summary>
        public static LocalisableString MobileDisclaimerOkButton => new TranslatableString(getKey(@"tumthai_next_mobile_disclaimer_ok_button"), @"Got it!");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
