// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using osu.Framework.IO.Network;
using osu.Framework.Logging;

namespace osu.Game.Online
{
    /// <summary>
    /// What a platform can tell <see cref="TumThaiClient"/> about the machine (osu.Desktop and osu.Android set it).
    /// </summary>
    public interface ITumThaiMachineInfo
    {
        /// <summary>osu!(stable)'s uninstall id on this machine (Windows: <c>HKCU\Software\osu!\UninstallID</c>), or null.</summary>
        string? StableUninstallId { get; }

        /// <summary>
        /// The operating system's id for this machine (Windows: <c>MachineGuid</c>, Linux: <c>/etc/machine-id</c>,
        /// Android: <c>ANDROID_ID</c>), or null.
        /// </summary>
        string? MachineId { get; }
    }

    /// <summary>
    /// TumThai's machine hashes, sent to a TomYum server on every request (its sign-in included) as the
    /// <see cref="HEADER"/> header, in osu!(stable)'s client hash format:
    /// <c>md5(install path):adapters:md5(adapters):md5(md5(uninstall id)):md5(md5(machine id)):</c>.
    /// The server uses them as stable uses its own: to tell TumThai from other lazer clients, and to refuse an
    /// account on the machine of a restricted or banned one. On Windows the adapters and (with stable installed)
    /// the uninstall id are the values stable sends too, so a lazer account is matched against stable ones.
    /// Only hashes leave the machine. Nothing is sent to a server that isn't TomYum (<see cref="IsTomYumServer"/>).
    /// </summary>
    public static class TumThaiClient
    {
        public const string HEADER = @"X-TumThai-Client";

        /// <summary>
        /// What this build understands beyond osu!lazer, for a TomYum server to send it: <c>playstyles</c>, the website's
        /// extra playstyles (<see cref="osu.Game.Online.API.Requests.Responses.APIPlayStyle"/>). Other clients get osu!lazer's own.
        /// </summary>
        public const string FEATURES_HEADER = @"X-TumThai-Features";

        public const string FEATURES = @"playstyles";

        /// <summary>
        /// Whether the server TumThai is connected to is a TomYum server: the built-in one, or a private
        /// server profile not marked as another kind of server. Off, TumThai is a plain lazer client there.
        /// </summary>
        public static bool IsTomYumServer { get; set; } = true;

        /// <summary>The platform's machine details; null leaves the uninstall id to TumThai's own and the machine id empty.</summary>
        public static ITumThaiMachineInfo? Machine { get; set; }

        /// <summary>Where TumThai keeps its own install id (set by the game once its storage is ready).</summary>
        public static string? InstallIdPath { get; set; }

        private static readonly object hashes_lock = new object();
        private static string? hashes;

        /// <summary>The header's value, or null when nothing is to be sent (not a TomYum server).</summary>
        public static string? Hashes
        {
            get
            {
                if (!IsTomYumServer)
                    return null;

                lock (hashes_lock)
                    return hashes ??= compute();
            }
        }

        /// <summary>Adds the header to <paramref name="request"/> (when connected to a TomYum server).</summary>
        public static void AddTo(WebRequest request)
        {
            if (Hashes is string value)
            {
                request.AddHeader(HEADER, value);
                request.AddHeader(FEATURES_HEADER, FEATURES);
            }
        }

        private static string compute()
        {
            string path = md5(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant());
            string adapters = adapterList();
            string uninstall = md5(md5(safe(() => Machine?.StableUninstallId) ?? installId()));
            string? machineId = safe(() => Machine?.MachineId);
            string machine = string.IsNullOrWhiteSpace(machineId) ? string.Empty : md5(md5(machineId.Trim()));

            return $@"{path}:{adapters}:{md5(adapters)}:{uninstall}:{machine}:";
        }

        /// <summary>The network adapters' addresses, each followed by a dot, as osu!(stable) lists them.</summary>
        private static string adapterList()
        {
            try
            {
                return string.Concat(NetworkInterface.GetAllNetworkInterfaces().Select(n => n.GetPhysicalAddress() + @"."));
            }
            catch (Exception)
            {
                // Android and sandboxed platforms may not list them.
                return string.Empty;
            }
        }

        /// <summary>TumThai's own random id, made on first use and kept in its storage.</summary>
        private static string installId()
        {
            string? file = InstallIdPath;

            try
            {
                if (file != null && File.Exists(file))
                {
                    string existing = File.ReadAllText(file).Trim();
                    if (Guid.TryParse(existing, out _))
                        return existing;
                }

                string created = Guid.NewGuid().ToString();
                if (file != null)
                    File.WriteAllText(file, created);
                return created;
            }
            catch (Exception e)
            {
                Logger.Log($"TumThai's install id couldn't be kept: {e.Message}", LoggingTarget.Network);
                return Guid.NewGuid().ToString();
            }
        }

        private static string? safe(Func<string?> get)
        {
            try
            {
                return get();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string md5(string value)
            => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
