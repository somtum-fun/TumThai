// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class OnlineSettingsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.OnlineSettings";

        /// <summary>
        /// "Online"
        /// </summary>
        public static LocalisableString OnlineSectionHeader => new TranslatableString(getKey(@"online_section_header"), @"Online");

        /// <summary>
        /// "Alerts and Privacy"
        /// </summary>
        public static LocalisableString AlertsAndPrivacyHeader => new TranslatableString(getKey(@"alerts_and_privacy_header"), @"Alerts and Privacy");

        /// <summary>
        /// "Show a notification when someone mentions your name"
        /// </summary>
        public static LocalisableString NotifyOnMentioned => new TranslatableString(getKey(@"notify_on_mentioned"), @"Show a notification when someone mentions your name");

        /// <summary>
        /// "Show a notification when you receive a private message"
        /// </summary>
        public static LocalisableString NotifyOnPrivateMessage => new TranslatableString(getKey(@"notify_on_private_message"), @"Show a notification when you receive a private message");

        /// <summary>
        /// "Show notification popups when friends change status"
        /// </summary>
        public static LocalisableString NotifyOnFriendPresenceChange => new TranslatableString(getKey(@"notify_on_friend_presence_change"), @"Show notification popups when friends change status");

        /// <summary>
        /// "Notifications will be shown when friends go online/offline."
        /// </summary>
        public static LocalisableString NotifyOnFriendPresenceChangeTooltip => new TranslatableString(getKey(@"notify_on_friend_presence_change_tooltip"), @"Notifications will be shown when friends go online/offline.");

        /// <summary>
        /// "Integrations"
        /// </summary>
        public static LocalisableString IntegrationsHeader => new TranslatableString(getKey(@"integrations_header"), @"Integrations");

        /// <summary>
        /// "Discord Rich Presence"
        /// </summary>
        public static LocalisableString DiscordRichPresence => new TranslatableString(getKey(@"discord_rich_presence"), @"Discord Rich Presence");

        /// <summary>
        /// "Web"
        /// </summary>
        public static LocalisableString WebHeader => new TranslatableString(getKey(@"web_header"), @"Web");

        /// <summary>
        /// "Warn about opening external links"
        /// </summary>
        public static LocalisableString ExternalLinkWarning => new TranslatableString(getKey(@"external_link_warning"), @"Warn about opening external links");

        /// <summary>
        /// "Prefer downloads without video"
        /// </summary>
        public static LocalisableString PreferNoVideo => new TranslatableString(getKey(@"prefer_no_video"), @"Prefer downloads without video");

        /// <summary>
        /// "Automatically download missing beatmaps"
        /// </summary>
        public static LocalisableString AutomaticallyDownloadMissingBeatmaps => new TranslatableString(getKey(@"automatically_download_missing_beatmaps"), @"Automatically download missing beatmaps");

        /// <summary>
        /// "Show explicit content in search results"
        /// </summary>
        public static LocalisableString ShowExplicitContent => new TranslatableString(getKey(@"show_explicit_content"), @"Show explicit content in search results");

        /// <summary>
        /// "Hide identifiable information"
        /// </summary>
        public static LocalisableString HideIdentifiableInformation => new TranslatableString(getKey(@"hide_identifiable_information"), @"Hide identifiable information");

        /// <summary>
        /// "Full"
        /// </summary>
        public static LocalisableString DiscordPresenceFull => new TranslatableString(getKey(@"discord_presence_full"), @"Full");

        /// <summary>
        /// "Off"
        /// </summary>
        public static LocalisableString DiscordPresenceOff => new TranslatableString(getKey(@"discord_presence_off"), @"Off");

        /// <summary>
        /// "Hide country flags"
        /// </summary>
        public static LocalisableString HideCountryFlags => new TranslatableString(getKey(@"hide_country_flags"), @"Hide country flags");

        /// <summary>
        /// "Custom server URL"
        /// </summary>
        public static LocalisableString CustomApiUrl => new TranslatableString(getKey(@"custom_api_url"), @"Custom server URL");

        /// <summary>
        /// "Host of the server to connect to (e.g. "osu.example.com"). Leave empty to use the default server."
        /// </summary>
        public static LocalisableString CustomApiUrlTooltip => new TranslatableString(getKey(@"custom_api_url_tooltip"), @"Host of the server to connect to (e.g. ""osu.example.com""). Leave empty to use the default server.");

        /// <summary>
        /// "A restart is required for this setting to take effect."
        /// </summary>
        public static LocalisableString CustomApiUrlRestartRequired => new TranslatableString(getKey(@"custom_api_url_restart_required"), @"A restart is required for this setting to take effect.");

        /// <summary>
        /// "Enter a valid host, optionally with a port (e.g. "osu.example.com" or "osu.example.com:8080")."
        /// </summary>
        public static LocalisableString CustomApiUrlInvalid => new TranslatableString(getKey(@"custom_api_url_invalid"), @"Enter a valid host, optionally with a port (e.g. ""osu.example.com"" or ""osu.example.com:8080"").");

        /// <summary>
        /// "Custom avatar URL"
        /// </summary>
        public static LocalisableString CustomAvatarUrl => new TranslatableString(getKey(@"custom_avatar_url"), @"Custom avatar URL");

        /// <summary>
        /// "Host serving user avatars (e.g. "a.example.com"). Looked up as https://{host}/{userId}. Leave empty to use the default."
        /// </summary>
        public static LocalisableString CustomAvatarUrlTooltip => new TranslatableString(getKey(@"custom_avatar_url_tooltip"), @"Host serving user avatars (e.g. ""a.example.com""). Looked up as https://{host}/{userId}. Leave empty to use the default.");

        /// <summary>
        /// "Server"
        /// </summary>
        public static LocalisableString ServerSectionHeader => new TranslatableString(getKey(@"server_section_header"), @"Server");

        /// <summary>
        /// "Connection"
        /// </summary>
        public static LocalisableString ServerConnectionHeader => new TranslatableString(getKey(@"server_connection_header"), @"Connection");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
