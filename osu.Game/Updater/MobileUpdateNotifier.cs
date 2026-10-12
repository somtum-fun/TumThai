// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Online.API;

namespace osu.Game.Updater
{
    /// <summary>
    /// An update manager that shows notifications if a newer release is detected for mobile platforms.
    /// Installation is left up to the user.
    /// </summary>
    public partial class MobileUpdateNotifier : UpdateManager
    {
        public override ReleaseStream? FixedReleaseStream => stream;

        private string version = null!;
        private ReleaseStream stream;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OsuGameBase game)
        {
            version = game.Version.Split('-').First();
            stream = Enum.TryParse(game.Version.Split('-').Last(), true, out ReleaseStream s) ? s : Configuration.ReleaseStream.Lazer;
        }

        // TumThai: our releases are listed in dl.somtum.fun's manifest (the website's download page uses it too),
        // not on ppy/osu's GitHub.
        private const string manifest_url = @"https://dl.somtum.fun/manifest.json";

        private const string download_page_url = @"https://osu.somtum.fun/home/download";

        protected override async Task<bool> PerformUpdateCheck(CancellationToken cancellationToken)
        {
            try
            {
                var manifestRequest = new OsuJsonWebRequest<DownloadManifest>(manifest_url);
                await manifestRequest.PerformAsync(cancellationToken).ConfigureAwait(false);

                var lazer = manifestRequest.ResponseObject?.Lazer;
                string? latestVersion = lazer?.Version;

                if (string.IsNullOrEmpty(latestVersion) || latestVersion == version)
                    return false;

                string? url = RuntimeInfo.OS == RuntimeInfo.Platform.Android ? lazer?.Android?.Url : null;

                if (url == null)
                    return false;

                Notifications.Post(new UpdateAvailableNotification(cancellationToken)
                {
                    Text = LocalisableString.Interpolate($"{NotificationsStrings.UpdateAvailable(version, latestVersion)}\n\n{NotificationsStrings.UpdateAvailableManualInstall}"),
                    Icon = FontAwesome.Solid.Download,
                    Activated = () =>
                    {
                        // the download page, as opening the .apk directly can make the download disappear on some devices.
                        host.OpenUrlExternally(download_page_url);
                        return true;
                    }
                });

                return true;
            }
            catch
            {
                // we shouldn't crash on a web failure. or any failure for the matter.
                return true;
            }
        }

        private class DownloadManifest
        {
            [JsonProperty(@"lazer")]
            public LazerRelease? Lazer { get; set; }
        }

        private class LazerRelease
        {
            [JsonProperty(@"version")]
            public string? Version { get; set; }

            [JsonProperty(@"android")]
            public LazerDownload? Android { get; set; }
        }

        private class LazerDownload
        {
            [JsonProperty(@"url")]
            public string? Url { get; set; }
        }
    }
}
