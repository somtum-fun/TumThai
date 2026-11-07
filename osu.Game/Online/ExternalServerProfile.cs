// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace osu.Game.Online
{
    /// <summary>
    /// Represents a saved private/external server connection profile.
    /// </summary>
    [Serializable]
    public class ExternalServerProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = string.Empty;

        public string ApiUrl { get; set; } = string.Empty;

        public string WebsiteUrl { get; set; } = string.Empty;

        public string AvatarUrl { get; set; } = string.Empty;

        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// The username signed in on this server, kept while another server is active.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The saved sign-in token for this server (only when "Stay signed in" is on), kept while another server is
        /// active so switching back doesn't need a new login.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Validates the profile fields, rejecting any <c>*.ppy.sh</c> host other than <c>dev.ppy.sh</c>.
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            string api = (ApiUrl ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(api))
            {
                errorMessage = "API / Server URL cannot be empty.";
                return false;
            }

            if (ServerHost.IsBlockedPpyHost(api)
                || ServerHost.IsBlockedPpyHost(WebsiteUrl)
                || ServerHost.IsBlockedPpyHost(AvatarUrl))
            {
                errorMessage = "Connections to *.ppy.sh (such as osu.ppy.sh) are blocked! Only dev.ppy.sh is allowed.";
                return false;
            }

            string derivedAvatar = SomtumEndpointConfiguration.ResolveAvatarHost(api, AvatarUrl);
            if (ServerHost.IsBlockedPpyHost(derivedAvatar) && !api.Contains(@"dev.ppy.sh", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Connections to *.ppy.sh (such as osu.ppy.sh) are blocked! Only dev.ppy.sh is allowed.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public static List<ExternalServerProfile> DeserializeList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<ExternalServerProfile>();

            try
            {
                return JsonConvert.DeserializeObject<List<ExternalServerProfile>>(json) ?? new List<ExternalServerProfile>();
            }
            catch
            {
                return new List<ExternalServerProfile>();
            }
        }

        public static string SerializeList(IEnumerable<ExternalServerProfile> profiles)
            => JsonConvert.SerializeObject(profiles);
    }
}
