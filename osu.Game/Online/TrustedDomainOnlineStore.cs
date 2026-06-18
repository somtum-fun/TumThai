// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Text.RegularExpressions;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;

namespace osu.Game.Online
{
    public sealed class TrustedDomainOnlineStore : OnlineStore
    {
        /// <summary>
        /// An additional registrable domain (e.g. "freedomdive.dev") to trust alongside ppy.sh.
        /// Derived from the configured custom server so its avatar/cover hosts (e.g. "a.freedomdive.dev")
        /// are not blocked. Null when no custom server is configured.
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
            customDomain = getRegistrableDomain(customServer);
            customAvatarDomain = getRegistrableDomain(customAvatarServer);
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

        /// <summary>
        /// Reduces a custom server value to its registrable domain (the last two labels, e.g.
        /// "lazer.freedomdive.dev" -> "freedomdive.dev") so that any sibling subdomain hosting
        /// avatars/covers is trusted. Returns null for empty input or bare IP addresses.
        /// </summary>
        private static string? getRegistrableDomain(string? customServer)
        {
            if (string.IsNullOrWhiteSpace(customServer))
                return null;

            // Strip scheme, any path, and a trailing port to leave a bare host.
            string host = Regex.Replace(customServer.Trim(), @"^\s*https?://", string.Empty, RegexOptions.IgnoreCase);

            int slash = host.IndexOf('/');
            if (slash >= 0)
                host = host.Substring(0, slash);

            int colon = host.IndexOf(':');
            if (colon >= 0)
                host = host.Substring(0, colon);

            host = host.Trim('.');

            if (host.Length == 0)
                return null;

            // Leave IP addresses untrusted for sub-host matching (an IP has no subdomains to extend trust to).
            if (Uri.CheckHostName(host) != UriHostNameType.Dns)
                return null;

            string[] labels = host.Split('.');
            if (labels.Length < 2)
                return null;

            return $@"{labels[^2]}.{labels[^1]}";
        }
    }
}
