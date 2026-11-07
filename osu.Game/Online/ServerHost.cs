// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Text.RegularExpressions;

namespace osu.Game.Online
{
    /// <summary>
    /// Turns a user-supplied server value, which may carry a scheme, a path or a port, into the bare host
    /// or base URL that the rest of the game needs. Kept in one place so every consumer of the custom
    /// server settings agrees on what a given value means.
    /// </summary>
    public static class ServerHost
    {
        private static readonly Regex scheme_prefix = new Regex(@"^\s*https?://", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Reduces a value to a bare host, dropping any scheme and path. Any port is kept.
        /// </summary>
        public static string Normalise(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string host = scheme_prefix.Replace(value.Trim(), string.Empty);

            int slash = host.IndexOf('/');
            if (slash >= 0)
                host = host.Substring(0, slash);

            return host.Trim().Trim('.', '/');
        }

        /// <summary>
        /// The registrable domain of a value: its last two labels, so "osu.example.com" gives
        /// "example.com". Used to decide that sibling subdomains of a configured server can be trusted.
        /// Null when there is no host, or when it is a bare IP address, which has no siblings to trust.
        /// </summary>
        public static string? RegistrableDomain(string? value)
        {
            string host = Normalise(value);

            int colon = host.IndexOf(':');
            if (colon >= 0)
                host = host.Substring(0, colon);

            if (host.Length == 0 || Uri.CheckHostName(host) != UriHostNameType.Dns)
                return null;

            string[] labels = host.Split('.');

            return labels.Length < 2 ? null : $@"{labels[^2]}.{labels[^1]}";
        }

        /// <summary>
        /// Turns a value into a base URL with no trailing slash, adding https:// when the user typed a
        /// bare host. Returns an empty string for an empty value.
        /// </summary>
        public static string ToBaseUrl(string? value)
        {
            string trimmed = (value ?? string.Empty).Trim().TrimEnd('/');

            if (trimmed.Length == 0)
                return string.Empty;

            if (trimmed.StartsWith(@"http://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(@"https://", StringComparison.OrdinalIgnoreCase))
                return trimmed;

            return $@"https://{trimmed}";
        }

        /// <summary>
        /// Returns true if the supplied URL or host points to <c>ppy.sh</c> or any <c>*.ppy.sh</c> subdomain
        /// other than <c>dev.ppy.sh</c> (and <c>*.dev.ppy.sh</c>), which is the only allowed ppy.sh domain.
        /// </summary>
        public static bool IsBlockedPpyHost(string? value)
        {
            string host = Normalise(value);

            int colon = host.IndexOf(':');
            if (colon >= 0)
                host = host.Substring(0, colon);

            host = host.Trim().TrimEnd('.');
            if (host.Length == 0)
                return false;

            if (host.Equals(@"dev.ppy.sh", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(@".dev.ppy.sh", StringComparison.OrdinalIgnoreCase))
                return false;

            if (host.Equals(@"ppy.sh", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(@".ppy.sh", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }
}
