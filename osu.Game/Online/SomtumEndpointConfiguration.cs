// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Online
{
    /// <summary>
    /// Endpoints for the osu!somtum private server, used when no custom server is configured
    /// via <see cref="Configuration.OsuSetting.CustomApiUrl"/>.
    /// </summary>
    public class SomtumEndpointConfiguration : EndpointConfiguration
    {
        /// <summary>
        /// The host serving the API and website. Also used to derive the set of trusted asset domains.
        /// </summary>
        public const string API_HOST = @"osu.sundei.eu";

        /// <summary>
        /// The host serving the realtime (SignalR) endpoints. This is a sibling host of
        /// <see cref="API_HOST"/> rather than a path on it, matching how the server is deployed.
        /// </summary>
        public const string SPECTATOR_HOST = @"spectator.sundei.eu";

        public SomtumEndpointConfiguration()
        {
            WebsiteUrl = APIUrl = $@"https://{API_HOST}";

            // the server validates against osu!'s public client credentials, the same pair
            // ProductionEndpointConfiguration sends. Leaving these empty makes the token endpoint reject
            // every sign-in attempt with "Check the `client_id` parameter" before any username is checked.
            APIClientID = @"5";
            APIClientSecret = @"FGc9GAtyHzeQDshWP5Ah7dega8hJACAJpQtw6OXk";

            SpectatorUrl = $@"https://{SPECTATOR_HOST}/spectator";
            MultiplayerUrl = $@"https://{SPECTATOR_HOST}/multiplayer";
            MetadataUrl = $@"https://{SPECTATOR_HOST}/metadata";

            // the server does not host a beatmap submission service; leaving this null disables submission.
            BeatmapSubmissionServiceUrl = null;
        }
    }
}
