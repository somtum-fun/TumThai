// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Online
{
    /// <summary>
    /// The one place that decides which server this client talks to, for both the built-in osu!somtum
    /// endpoints and a custom server configured via <see cref="Configuration.OsuSetting.CustomApiUrl"/>.
    /// Changing servers should only ever mean editing the constants below.
    /// </summary>
    public class SomtumEndpointConfiguration : EndpointConfiguration
    {
        /// <summary>
        /// The host serving the API and website.
        /// </summary>
        public const string API_HOST = @"osu.blueskychan.dev";

        /// <summary>
        /// The host serving the realtime (SignalR) endpoints. A sibling of <see cref="API_HOST"/> rather
        /// than a path on it, matching how the server is deployed.
        /// </summary>
        public const string SPECTATOR_HOST = @"spectator.blueskychan.dev";

        /// <summary>
        /// The host serving user avatars. Kept consistent with <see cref="ResolveAvatarHost"/>, which
        /// derives the same value from <see cref="API_HOST"/>.
        /// </summary>
        public const string AVATAR_HOST = @"a.blueskychan.dev";

        /// <summary>
        /// Endpoints for the built-in server.
        /// </summary>
        public SomtumEndpointConfiguration()
            : this(null)
        {
        }

        /// <param name="customApiServer">
        /// A custom server, as a bare host optionally carrying a scheme or port. Empty or null selects the
        /// built-in server.
        /// </param>
        public SomtumEndpointConfiguration(string? customApiServer)
        {
            // the server validates against osu!'s public client credentials, the same pair
            // ProductionEndpointConfiguration sends. Leaving these empty makes the token endpoint reject
            // every sign-in attempt with "Check the `client_id` parameter" before any username is checked.
            APIClientID = @"5";
            APIClientSecret = @"FGc9GAtyHzeQDshWP5Ah7dega8hJACAJpQtw6OXk";

            string customUrl = ServerHost.ToBaseUrl(customApiServer);

            if (customUrl.Length == 0)
            {
                WebsiteUrl = APIUrl = $@"https://{API_HOST}";

                SpectatorUrl = $@"https://{SPECTATOR_HOST}/spectator";
                MultiplayerUrl = $@"https://{SPECTATOR_HOST}/multiplayer";
                MetadataUrl = $@"https://{SPECTATOR_HOST}/metadata";

                // the server does not host a beatmap submission service; null disables submission.
                BeatmapSubmissionServiceUrl = null;
                return;
            }

            APIUrl = customUrl;

            // prefer a separate website host where the pattern is recognised, so chat and external links
            // resolve against the website rather than the JSON-only API host.
            WebsiteUrl = DeriveWebsiteUrl(customUrl);

            // an arbitrary server cannot be assumed to have a sibling realtime host, so use the paths that
            // a single-host deployment exposes.
            SpectatorUrl = $@"{customUrl}/signalr/spectator";
            MultiplayerUrl = $@"{customUrl}/signalr/multiplayer";
            MetadataUrl = $@"{customUrl}/signalr/metadata";
            BeatmapSubmissionServiceUrl = $@"{customUrl}/beatmap-submission";
        }

        /// <summary>
        /// The host to look up avatars on when the API does not supply an explicit URL. An explicitly
        /// configured avatar host always wins. Otherwise the host follows whichever server is configured,
        /// because every deployment involved serves avatars from an "a." sibling of its registrable domain
        /// (a.ppy.sh for osu.ppy.sh, <see cref="AVATAR_HOST"/> for <see cref="API_HOST"/>).
        /// </summary>
        public static string ResolveAvatarHost(string? customApiServer, string? customAvatarServer)
        {
            string configured = ServerHost.Normalise(customAvatarServer);

            if (configured.Length > 0)
                return configured;

            string? domain = ServerHost.RegistrableDomain(customApiServer);

            return domain == null ? AVATAR_HOST : $@"a.{domain}";
        }

        /// <summary>
        /// Derives a website URL from an API URL, handling the two common private-server patterns of an
        /// "api." prefix or an "-api" suffix on the first label. Returns the input unchanged when neither
        /// applies, i.e. when the API and website share a host.
        /// </summary>
        public static string DeriveWebsiteUrl(string apiUrl)
        {
            if (string.IsNullOrEmpty(apiUrl))
                return apiUrl;

            try
            {
                var uri = new Uri(apiUrl);
                string host = uri.Host;
                string newHost = host;

                if (host.Contains(@"-api.", StringComparison.OrdinalIgnoreCase))
                    newHost = host.Replace(@"-api.", @".", StringComparison.OrdinalIgnoreCase);
                else if (host.StartsWith(@"api.", StringComparison.OrdinalIgnoreCase))
                    newHost = host.Substring(4);

                if (newHost == host)
                    return apiUrl;

                return $@"{uri.Scheme}://{newHost}";
            }
            catch (UriFormatException)
            {
                return apiUrl;
            }
        }
    }
}
