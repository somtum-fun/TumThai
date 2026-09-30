// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;

namespace osu.Game.Online
{
    public sealed class TrustedDomainOnlineStore : OnlineStore
    {
        /// <summary>
        /// An additional registrable domain (e.g. "example.com") to trust alongside ppy.sh.
        /// Derived from the configured server so its avatar/cover hosts (e.g. "a.example.com")
        /// are not blocked. Null when the value supplied is empty or a bare IP address.
        /// </summary>
        private readonly string? customDomain;

        /// <summary>
        /// Additional registrable domain derived from the configured custom avatar host, so a custom avatar
        /// server on a different domain than the API server is still trusted.
        /// </summary>
        private readonly string? customAvatarDomain;

        /// <param name="customServer">
        /// The configured custom server value (a bare host, optionally with scheme/port). May be null or empty.
        /// </param>
        /// <param name="customAvatarServer">
        /// The configured custom avatar host (a bare host, optionally with scheme/port). May be null or empty.
        /// </param>
        public TrustedDomainOnlineStore(string? customServer = null, string? customAvatarServer = null)
        {
            customDomain = ServerHost.RegistrableDomain(customServer);
            customAvatarDomain = ServerHost.RegistrableDomain(customAvatarServer);
        }

        protected override string GetLookupUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && isTrusted(uri.Host))
                return url;

            Logger.Log($@"Blocking resource lookup from external website: {url}", LoggingTarget.Network, LogLevel.Important);
            return string.Empty;
        }

        private bool isTrusted(string host)
        {
            if (host.EndsWith(@".ppy.sh", StringComparison.OrdinalIgnoreCase))
                return true;

            if (matchesDomain(host, customDomain) || matchesDomain(host, customAvatarDomain))
                return true;

            return false;
        }

        private static bool matchesDomain(string host, string? domain)
            => !string.IsNullOrEmpty(domain)
               && (host.Equals(domain, StringComparison.OrdinalIgnoreCase)
                   || host.EndsWith($@".{domain}", StringComparison.OrdinalIgnoreCase));

    }
}
