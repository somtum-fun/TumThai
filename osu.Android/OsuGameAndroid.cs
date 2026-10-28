// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Android.App;
using Android.Content.PM;
using Microsoft.Maui.Devices;
using osu.Android.Performance;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Performance;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Updater;
using osu.Game.Utils;
using osuTK;

namespace osu.Android
{
    public partial class OsuGameAndroid : OsuGame
    {
        [Cached]
        private readonly OsuGameActivity gameActivity;

        [Cached(typeof(IHighPerformanceSessionManager))]
        private readonly AndroidHighPerformanceSessionManager highPerformanceSessionManager = new AndroidHighPerformanceSessionManager();

        private readonly PackageInfo packageInfo;

        private Bindable<DisplayRefreshRateMode>? displayRefreshRate;
        private Bindable<bool>? androidLowLatencyAudio;

        public override Vector2 ScalingContainerTargetDrawSize => new Vector2(1024, 1024 * DrawHeight / DrawWidth);

        public OsuGameAndroid(OsuGameActivity activity)
            : base(null)
        {
            gameActivity = activity;
            packageInfo = Application.Context.ApplicationContext!.PackageManager!.GetPackageInfo(Application.Context.ApplicationContext.PackageName!, 0).AsNonNull();
        }

        public override string Version
        {
            get
            {
                if (!IsDeployedBuild)
                    return @"local " + (DebugUtils.IsDebugBuild ? @"debug" : @"release");

                return packageInfo.VersionName.AsNonNull();
            }
        }

        public override Version AssemblyVersion => new Version(packageInfo.VersionName.AsNonNull().Split('-').First());

        protected override void LoadComplete()
        {
            base.LoadComplete();

            UserPlayingState.BindValueChanged(state =>
            {
                updateOrientation();
                gameActivity.SetGameplayTouchProtection(state.NewValue == LocalUserPlayingState.Playing);
            }, true);

            displayRefreshRate = LocalConfig.GetBindable<DisplayRefreshRateMode>(OsuSetting.DisplayRefreshRate);
            displayRefreshRate.BindValueChanged(v =>
            {
                float resolvedHz = gameActivity.ApplyDisplayRefreshRate((int)v.NewValue);
                highPerformanceSessionManager.UpdateDisplayRefreshRate(resolvedHz);
            }, true);

            androidLowLatencyAudio = LocalConfig.GetBindable<bool>(OsuSetting.AndroidLowLatencyAudio);
            androidLowLatencyAudio.BindValueChanged(v =>
            {
                AndroidAudioOptimizer.ScheduleApplyOnAudioThread(gameActivity, Host?.AudioThread, !PotatoMode.Active && v.NewValue);
            }, true);

            Audio.OnNewDevice += onAudioDeviceChanged;
            Scheduler.AddDelayed(() => onAudioDeviceChanged(string.Empty), 750);

            AndroidAudioOptimizer.RegisterRouteMonitor(gameActivity, Host?.AudioThread, () => !PotatoMode.Active && (androidLowLatencyAudio?.Value ?? true));
        }

        private void onAudioDeviceChanged(string _)
        {
            AndroidAudioOptimizer.ScheduleApplyOnAudioThread(gameActivity, Host?.AudioThread, !PotatoMode.Active && (androidLowLatencyAudio?.Value ?? true));
        }

        protected override void Update()
        {
            base.Update();
            highPerformanceSessionManager.ReportFrameIfNeeded();
        }

        protected override void ScreenChanged(IOsuScreen? current, IOsuScreen? newScreen)
        {
            base.ScreenChanged(current, newScreen);

            if (newScreen != null)
                updateOrientation();
        }

        private void updateOrientation()
        {
            if (ScreenStack.CurrentScreen is not IOsuScreen currentScreen)
                return;

            var orientation = MobileUtils.GetOrientation(this, currentScreen, gameActivity.IsTablet);

            switch (orientation)
            {
                case MobileUtils.Orientation.Locked:
                    gameActivity.RequestedOrientation = ScreenOrientation.Locked;
                    break;

                case MobileUtils.Orientation.Portrait:
                    gameActivity.RequestedOrientation = ScreenOrientation.Portrait;
                    break;

                case MobileUtils.Orientation.Default:
                    gameActivity.RequestedOrientation = gameActivity.DefaultOrientation;
                    break;
            }
        }

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);
            host.Window.CursorState |= CursorState.Hidden;
            highPerformanceSessionManager.AttachHost(host);
        }

        internal void OnActivityPaused() => highPerformanceSessionManager.OnActivityPaused();

        internal void OnActivityResumed() => highPerformanceSessionManager.OnActivityResumed();

        protected override UpdateManager CreateUpdateManager() => new MobileUpdateNotifier();

        protected override BatteryInfo CreateBatteryInfo() => new AndroidBatteryInfo();

        protected override void Dispose(bool isDisposing)
        {
            if (IsLoaded)
                Audio.OnNewDevice -= onAudioDeviceChanged;

            base.Dispose(isDisposing);
            highPerformanceSessionManager.Dispose();
        }

        private class AndroidBatteryInfo : BatteryInfo
        {
            public override double? ChargeLevel => Battery.ChargeLevel;

            public override bool OnBattery => Battery.PowerSource == BatteryPowerSource.Battery;
        }
    }
}
