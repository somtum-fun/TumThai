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
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Overlays.Notifications;

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
            // TumThai: TumThai!Next's versions end in -next.
            stream = game.Version.EndsWith(@"-next", StringComparison.Ordinal) ? Configuration.ReleaseStream.Tachyon : Configuration.ReleaseStream.Lazer;
        }

        // TumThai: our releases are listed in dl.somtum.fun's manifest (the website's download page uses it too),
        // not on ppy/osu's GitHub.
        private const string manifest_url = SomtumEndpointConfiguration.MANIFEST_URL;

        private const string download_page_url = SomtumEndpointConfiguration.DOWNLOAD_PAGE_URL;

        protected override async Task<bool> PerformUpdateCheck(CancellationToken cancellationToken)
        {
            try
            {
                var manifestRequest = new OsuJsonWebRequest<DownloadManifest>(manifest_url);
                await manifestRequest.PerformAsync(cancellationToken).ConfigureAwait(false);

                // TumThai: TumThai!Next is listed as lazer_next.
                var lazer = stream == Configuration.ReleaseStream.Tachyon ? manifestRequest.ResponseObject?.LazerNext : manifestRequest.ResponseObject?.Lazer;
                string? latestVersion = lazer?.Version?.Split('-').First();

                if (string.IsNullOrEmpty(latestVersion) || latestVersion == version)
                    return false;

                // TumThai: on Linux and macOS too (no self-update there). Started from the Somtum Launcher,
                // the launcher installs the new version the next time it starts TumThai.
                bool android = RuntimeInfo.OS == RuntimeInfo.Platform.Android;
                bool viaLauncher = Environment.GetEnvironmentVariable(@"OSU_EXTERNAL_UPDATE_PROVIDER") == @"somtum-launcher";

                if (android && lazer?.Android?.Url == null)
                    return false;

                if (viaLauncher)
                {
                    Notifications.Post(new SimpleNotification
                    {
                        Text = LocalisableString.Interpolate($"{NotificationsStrings.UpdateAvailable(version, latestVersion)}\n\nClose TumThai and start it again from the Somtum Launcher to update."),
                        Icon = FontAwesome.Solid.Download,
                    });
                    return true;
                }

                Notifications.Post(new UpdateAvailableNotification(cancellationToken)
                {
                    Text = LocalisableString.Interpolate($"{NotificationsStrings.UpdateAvailable(version, latestVersion)}\n\n{NotificationsStrings.UpdateAvailableManualInstall}"),
                    Icon = FontAwesome.Solid.Download,
                    Activated = () =>
                    {
                        // the download page (on Android, opening the .apk directly can make the download disappear on some devices).
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

            [JsonProperty(@"lazer_next")]
            public LazerRelease? LazerNext { get; set; }
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
