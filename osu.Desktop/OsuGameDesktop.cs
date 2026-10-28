// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;
using osu.Desktop.IPC;
using osu.Desktop.MacOS;
using osu.Desktop.Performance;
using osu.Desktop.Security;
using osu.Desktop.Updater;
using osu.Desktop.Windows;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.IO;
using osu.Game.IPC;
using osu.Game.Performance;
using osu.Game.Updater;
using osu.Game.Utils;

namespace osu.Desktop
{
    internal partial class OsuGameDesktop : OsuGame
    {
        private OsuSchemeLinkIPCChannel? osuSchemeLinkIPCChannel;
        private ArchiveImportIPCChannel? archiveImportIPCChannel;

        [Cached(typeof(IHighPerformanceSessionManager))]
        private readonly DesktopHighPerformanceSessionManager highPerformanceSessionManager = new DesktopHighPerformanceSessionManager();

        public bool IsFirstRun { get; init; }

        public bool EnableWebSocketServer { get; init; }

        public OsuGameDesktop(string[]? args = null)
            : base(args)
        {
        }

        public override StableStorage? GetStorageForStableInstall()
        {
            try
            {
                if (Host is DesktopGameHost desktopHost)
                {
                    string? stablePath = getStableInstallPath();
                    if (!string.IsNullOrEmpty(stablePath))
                        return new StableStorage(stablePath, desktopHost);
                }
            }
            catch (Exception)
            {
                Logger.Log("Could not find a stable install", LoggingTarget.Runtime, LogLevel.Important);
            }

            return null;
        }

        private string? getStableInstallPath()
        {
            static bool checkExists(string p) => Directory.Exists(Path.Combine(p, "Songs")) || File.Exists(Path.Combine(p, "osu!.cfg"));

            string? stableInstallPath;

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    stableInstallPath = getStableInstallPathFromRegistry("osustable.File.osz");

                    if (!string.IsNullOrEmpty(stableInstallPath) && checkExists(stableInstallPath))
                        return stableInstallPath;

                    stableInstallPath = getStableInstallPathFromRegistry("osu!");

                    if (!string.IsNullOrEmpty(stableInstallPath) && checkExists(stableInstallPath))
                        return stableInstallPath;
                }
                catch
                {
                }
            }

            stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"osu!");
            if (checkExists(stableInstallPath))
                return stableInstallPath;

            stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".osu");
            if (checkExists(stableInstallPath))
                return stableInstallPath;

            return null;
        }

        [SupportedOSPlatform("windows")]
        private string? getStableInstallPathFromRegistry(string progId)
        {
            using (RegistryKey? key = Registry.ClassesRoot.OpenSubKey(progId))
                return key?.OpenSubKey(WindowsAssociationManager.SHELL_OPEN_COMMAND)?.GetValue(string.Empty)?.ToString()?.Split('"')[1].Replace("osu!.exe", "");
        }

        public static bool IsPackageManaged => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OSU_EXTERNAL_UPDATE_PROVIDER"));

        protected override UpdateManager CreateUpdateManager()
        {
            // If this is the first time we've run the game, ie it is being installed,
            // reset the user's release stream specified by the installation target.
            //
            // This ensures that if a user is trying to recover from a failed startup, it will keep them
            // on the stream which is imminently being reinstalled.
            if (IsFirstRun)
                LocalConfig.SetValue(OsuSetting.ReleaseStream, Version.Contains("-next") ? ReleaseStream.Tachyon : ReleaseStream.Lazer); // TumThai: TumThai!Next's versions end in -next

            // TumThai: anywhere Velopack doesn't update us (started by the Somtum Launcher, or Linux and macOS,
            // which have no update feed), dl.somtum.fun's manifest says when there's a new version.
            if (IsPackageManaged || RuntimeInfo.OS != RuntimeInfo.Platform.Windows)
                return new MobileUpdateNotifier();

            return new VelopackUpdateManager();
        }

        public override bool RestartAppWhenExited()
        {
            if (IsPackageManaged || !IsDeployedBuild)
                return false;

            RestartOnExitAction = () => Velopack.UpdateExe.Start(waitPid: (uint)Environment.ProcessId);
            return true;
        }

        protected override void LoadComplete()
        {
            // this is done before `base.LoadComplete` so that the game can immediately register data sources.
            if (EnableWebSocketServer)
            {
                var provider = new OsuWebSocketProvider();
                Add(provider);
                Dependencies.CacheAs<IWebSocketProvider>(provider);
            }

            base.LoadComplete();

            LoadComponentAsync(new DiscordRichPresence(), Add);

            switch (RuntimeInfo.OS)
            {
                case RuntimeInfo.Platform.Windows:
                    LoadComponentAsync(new GameplayWinKeyBlocker(), Add);
                    break;

                case RuntimeInfo.Platform.macOS when !IsPackageManaged && IsDeployedBuild:
                    if (!IsPackageManaged && IsDeployedBuild)
                        LoadComponentAsync(new MacOSAppLocationChecker(), Add);
                    break;
            }

            LoadComponentAsync(new ElevatedPrivilegesChecker(), Add);

            osuSchemeLinkIPCChannel = new OsuSchemeLinkIPCChannel(Host, this);
            archiveImportIPCChannel = new ArchiveImportIPCChannel(Host, this);
        }

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);

            highPerformanceSessionManager.AttachHost(host);

            // Apple operating systems use a better icon provided via external assets.
            if (!RuntimeInfo.IsApple)
            {
                var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetType(), "lazer.ico");
                if (iconStream != null)
                    host.Window.SetIconFromStream(iconStream);
            }

            host.Window.Title = Name;
        }

        protected override BatteryInfo CreateBatteryInfo() => FrameworkEnvironment.UseSDL3 ? new SDL3BatteryInfo() : new SDL2BatteryInfo();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            highPerformanceSessionManager.Dispose();
            osuSchemeLinkIPCChannel?.Dispose();
            archiveImportIPCChannel?.Dispose();
        }
    }
}
