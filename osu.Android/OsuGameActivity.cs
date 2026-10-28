// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using osu.Android.Performance;
using osu.Framework.Android;
using osu.Framework.Logging;
using osu.Game.Database;
using Debug = System.Diagnostics.Debug;
using Uri = Android.Net.Uri;

namespace osu.Android
{
    [Activity(ConfigurationChanges = DEFAULT_CONFIG_CHANGES, Exported = true, LaunchMode = DEFAULT_LAUNCH_MODE, MainLauncher = true)]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, Label = "Import beatmap", DataScheme = "content", DataPathPattern = ".*\\\\.osz", DataHost = "*",
        DataMimeType = "*/*")]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, Label = "Import skin", DataScheme = "content", DataPathPattern = ".*\\\\.osk", DataHost = "*",
        DataMimeType = "*/*")]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, Label = "Import replay", DataScheme = "content", DataPathPattern = ".*\\\\.osr", DataHost = "*",
        DataMimeType = "*/*")]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, Label = "Import beatmap", DataScheme = "content", DataMimeType = "application/x-osu-beatmap-archive")]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, Label = "Import skin", DataScheme = "content", DataMimeType = "application/x-osu-skin-archive")]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, Label = "Import replay", DataScheme = "content", DataMimeType = "application/x-osu-replay")]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, Label = "Import file", DataScheme = "content", DataMimeTypes = new[]
    {
        "application/zip",
        "application/octet-stream",
        "application/download",
        "application/x-zip",
        "application/x-zip-compressed",
    })]
    [IntentFilter(new[] { Intent.ActionSend, Intent.ActionSendMultiple }, Categories = new[] { Intent.CategoryDefault }, Label = "Import", DataMimeTypes = new[]
    {
        "application/zip",
        "application/octet-stream",
        "application/download",
        "application/x-zip",
        "application/x-zip-compressed",
        // newer official mime types (see https://osu.ppy.sh/wiki/en/osu%21_File_Formats).
        "application/x-osu-beatmap-archive",
        "application/x-osu-skin-archive",
        "application/x-osu-replay",
    })]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryBrowsable, Intent.CategoryDefault }, DataSchemes = new[] { "osu", "osump" })]
    public class OsuGameActivity : AndroidGameActivity
    {
        private static readonly string[] osu_url_schemes = { "osu", "osump" };

        // Android caps vertical system-gesture exclusion height to 200dp per edge.
        private const float max_edge_exclusion_dp = 200f;
        private const float edge_exclusion_width_dp = 48f;
        private const float bottom_exclusion_height_dp = 40f;

        /// <summary>
        /// The default screen orientation.
        /// </summary>
        /// <remarks>Adjusted on startup to match expected UX for the current device type (phone/tablet).</remarks>
        public ScreenOrientation DefaultOrientation = ScreenOrientation.Unspecified;

        public new bool IsTablet { get; private set; }

        private readonly OsuGameAndroid game;

        private bool gameCreated;
        private SurfaceView? gameSurfaceView;
        private SurfaceFrameRateCallback? surfaceCallback;
        private int lastRequestedRefreshHz;
        private float activeTargetRefreshRate;
        private bool gameplayTouchProtectionActive;

        protected override Framework.Game CreateGame()
        {
            if (gameCreated)
                throw new InvalidOperationException("Framework tried to create a game twice.");

            gameCreated = true;
            return game;
        }

        public OsuGameActivity()
        {
            game = new OsuGameAndroid(this);
        }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Configure BASS's built-in AAudio low-latency parameters BEFORE base.OnCreate starts SDL and calls Bass.Init.
            AndroidAudioOptimizer.ConfigureBeforeStartup(this);

            base.OnCreate(savedInstanceState);

            // OnNewIntent() only fires for an activity if it's *re-launched* while it's on top of the activity stack.
            // on first launch we still have to fire manually.
            // reference: https://developer.android.com/reference/android/app/Activity#onNewIntent(android.content.Intent)
            handleIntent(Intent);

            Debug.Assert(Window != null);

            Window.AddFlags(WindowManagerFlags.Fullscreen);
            Window.AddFlags(WindowManagerFlags.KeepScreenOn);
            configureImmersiveWindow();

            Debug.Assert(WindowManager?.DefaultDisplay != null);
            Debug.Assert(Resources?.DisplayMetrics != null);

            Point displaySize = new Point();
#pragma warning disable CA1422 // GetSize is deprecated
            WindowManager.DefaultDisplay.GetSize(displaySize);
#pragma warning restore CA1422
            float smallestWidthDp = Math.Min(displaySize.X, displaySize.Y) / Resources.DisplayMetrics.Density;
            IsTablet = smallestWidthDp >= 600f;

            RequestedOrientation = DefaultOrientation = IsTablet ? ScreenOrientation.FullUser : ScreenOrientation.SensorLandscape;

            ensureSurfaceConfigured();
            ApplyDisplayRefreshRate(lastRequestedRefreshHz);

            // Currently (SDK 6.0.200), BundleAssemblies is not runnable for net6-android.
            // The assembly files are not available as files either after native AOT.
            // Manually load them so that they can be loaded by RulesetStore.loadFromAppDomain.
            // REMEMBER to fully uninstall previous version every time when investigating this!
            // Don't forget osu.Game.Tests.Android too.
            Assembly.Load("osu.Game.Rulesets.Osu");
            Assembly.Load("osu.Game.Rulesets.Taiko");
            Assembly.Load("osu.Game.Rulesets.Catch");
            Assembly.Load("osu.Game.Rulesets.Mania");
        }

        protected override void OnResume()
        {
            base.OnResume();
            game.OnActivityResumed();
            ApplyDisplayRefreshRate(lastRequestedRefreshHz);
        }

        protected override void OnPause()
        {
            game.OnActivityPaused();
            base.OnPause();
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);

            if (!hasFocus)
                return;

            configureImmersiveWindow();
            ensureSurfaceConfigured();
            applySurfaceFrameRateHint();
            updateGestureExclusionRects();
        }

        public override bool DispatchTouchEvent(MotionEvent? ev)
        {
            // Bypass Android Choreographer VSync touch batching so high-rate touch samples
            // reach SDL / osu!framework's InputThread immediately instead of waiting up to one frame.
            if (ev != null && ev.ActionMasked == MotionEventActions.Down)
                (ensureSurfaceConfigured() ?? Window?.DecorView)?.RequestUnbufferedDispatch(ev);

            return base.DispatchTouchEvent(ev);
        }

        public override bool DispatchGenericMotionEvent(MotionEvent? ev)
        {
            // Also unbuffer stylus hover/pen strokes and external mouse/touchpad motion.
            if (ev != null)
            {
                MotionEventActions action = ev.ActionMasked;
                if (action == MotionEventActions.HoverEnter || action == MotionEventActions.ButtonPress)
                    (ensureSurfaceConfigured() ?? Window?.DecorView)?.RequestUnbufferedDispatch(ev);
            }

            return base.DispatchGenericMotionEvent(ev);
        }

        /// <summary>
        /// Locks the window display mode and SurfaceFlinger frame-rate hint to the requested refresh rate
        /// (or the panel's maximum native refresh rate when <paramref name="preferredHz"/> is 0) so LTPO panels
        /// do not step down refresh rate mid-slider.
        /// </summary>
        internal float ApplyDisplayRefreshRate(int preferredHz)
        {
            lastRequestedRefreshHz = preferredHz;

            Display.Mode? selectedMode = selectBestDisplayMode(preferredHz, out float resolvedHz);
            activeTargetRefreshRate = resolvedHz;

            RunOnUiThread(() =>
            {
                try
                {
                    if (Window != null && selectedMode != null)
                    {
                        WindowManagerLayoutParams? attrs = Window.Attributes;

                        if (attrs != null && attrs.PreferredDisplayModeId != selectedMode.ModeId)
                        {
                            attrs.PreferredDisplayModeId = selectedMode.ModeId;
                            Window.Attributes = attrs;
                        }
                    }

                    ensureSurfaceConfigured();
                    applySurfaceFrameRateHint();

                    Logger.Log($"[AndroidDisplay] Applied display mode (requested={preferredHz} Hz, resolved={resolvedHz:0.##} Hz, modeId={selectedMode?.ModeId ?? 0}).",
                        LoggingTarget.Performance);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[AndroidDisplay] Failed to apply display refresh rate: {ex.Message}", LoggingTarget.Performance);
                }
            });

            return resolvedHz;
        }

        /// <summary>
        /// Excludes bottom and lower-edge gesture zones during active gameplay to prevent One UI / Pixel gesture
        /// navigation from holding back edge taps for 40-100 ms, restoring normal swipe gestures when paused or in menus.
        /// </summary>
        internal void SetGameplayTouchProtection(bool active)
        {
            gameplayTouchProtectionActive = active;
            RunOnUiThread(updateGestureExclusionRects);
        }

        protected override void OnNewIntent(Intent? intent) => handleIntent(intent);

        private void configureImmersiveWindow()
        {
            if (Window == null)
                return;

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
                configureImmersiveWindowApi30(Window);
        }

        [SupportedOSPlatform("android30.0")]
        private static void configureImmersiveWindowApi30(Window window)
        {
            try
            {
                if (!OperatingSystem.IsAndroidVersionAtLeast(35))
                    window.SetDecorFitsSystemWindows(false);

                IWindowInsetsController? insetsController = window.InsetsController;
                if (insetsController == null)
                    return;

                insetsController.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
                insetsController.Hide(WindowInsets.Type.SystemBars());
            }
            catch
            {
            }
        }

        private Display.Mode? selectBestDisplayMode(int preferredHz, out float resolvedHz)
        {
            resolvedHz = preferredHz > 0 ? preferredHz : 120f;

            try
            {
                Display? display = getActivityDisplay();
                if (display == null)
                    return null;

                resolvedHz = display.RefreshRate;

                Display.Mode[]? modes = display.GetSupportedModes();
                if (modes == null || modes.Length == 0)
                    return null;

                Display.Mode? currentMode = display.GetMode();
                long maxPixels = 0;

                foreach (Display.Mode mode in modes)
                {
                    long pixels = (long)mode.PhysicalWidth * mode.PhysicalHeight;
                    if (pixels > maxPixels)
                        maxPixels = pixels;
                }

                Display.Mode? bestMode = null;

                foreach (Display.Mode mode in modes)
                {
                    if (currentMode != null
                        ? (mode.PhysicalWidth != currentMode.PhysicalWidth || mode.PhysicalHeight != currentMode.PhysicalHeight)
                        : ((long)mode.PhysicalWidth * mode.PhysicalHeight != maxPixels))
                        continue;

                    if (bestMode == null)
                    {
                        bestMode = mode;
                        continue;
                    }

                    if (preferredHz <= 0)
                    {
                        if (mode.RefreshRate > bestMode.RefreshRate)
                            bestMode = mode;
                    }
                    else
                    {
                        float currentDiff = Math.Abs(mode.RefreshRate - preferredHz);
                        float bestDiff = Math.Abs(bestMode.RefreshRate - preferredHz);

                        if (currentDiff < bestDiff - 0.1f || (Math.Abs(currentDiff - bestDiff) <= 0.1f && mode.RefreshRate > bestMode.RefreshRate))
                            bestMode = mode;
                    }
                }

                if (bestMode != null)
                    resolvedHz = bestMode.RefreshRate;

                return bestMode;
            }
            catch
            {
                return null;
            }
        }

        private Display? getActivityDisplay()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
                return Display;

#pragma warning disable CA1422 // DefaultDisplay is deprecated on API 30+
            return WindowManager?.DefaultDisplay;
#pragma warning restore CA1422
        }

        private SurfaceView? ensureSurfaceConfigured()
        {
            if (gameSurfaceView != null && gameSurfaceView.Handle != IntPtr.Zero)
                return gameSurfaceView;

            View? decorView = Window?.DecorView;
            if (decorView == null)
                return null;

            SurfaceView? found = findSurfaceView(decorView);
            if (found == null)
                return null;

            gameSurfaceView = found;

            if (surfaceCallback == null && found.Holder != null)
            {
                surfaceCallback = new SurfaceFrameRateCallback(this);
                found.Holder.AddCallback(surfaceCallback);
            }

            return found;
        }

        private static SurfaceView? findSurfaceView(View root)
        {
            if (root is SurfaceView sv)
                return sv;

            if (root is not ViewGroup group)
                return null;

            for (int i = 0; i < group.ChildCount; i++)
            {
                View? child = group.GetChildAt(i);
                if (child == null)
                    continue;

                SurfaceView? nested = findSurfaceView(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private void applySurfaceFrameRateHint()
        {
            if (activeTargetRefreshRate <= 0 || !OperatingSystem.IsAndroidVersionAtLeast(30))
                return;

            Surface? surface = gameSurfaceView?.Holder?.Surface;
            if (surface == null || !surface.IsValid)
                return;

            try
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(31))
                    setSurfaceFrameRateApi31(surface, activeTargetRefreshRate);
                else
                    setSurfaceFrameRateApi30(surface, activeTargetRefreshRate);
            }
            catch
            {
            }
        }

        [SupportedOSPlatform("android30.0")]
        private static void setSurfaceFrameRateApi30(Surface surface, float frameRate)
        {
            // 0 = FRAME_RATE_COMPATIBILITY_DEFAULT
            surface.SetFrameRate(frameRate, 0);
        }

        [SupportedOSPlatform("android31.0")]
        private static void setSurfaceFrameRateApi31(Surface surface, float frameRate)
        {
            // 0 = FRAME_RATE_COMPATIBILITY_DEFAULT, 0 = CHANGE_FRAME_RATE_ONLY_IF_SEAMLESS
            surface.SetFrameRate(frameRate, 0, 0);
        }

        private void updateGestureExclusionRects()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                return;

            View? decorView = Window?.DecorView;
            if (decorView == null)
                return;

            applyGestureExclusionRectsApi29(decorView);
        }

        [SupportedOSPlatform("android29.0")]
        private void applyGestureExclusionRectsApi29(View decorView)
        {
            try
            {
                if (!gameplayTouchProtectionActive)
                {
                    decorView.SystemGestureExclusionRects = new List<Rect>();
                    return;
                }

                int width = decorView.Width;
                int height = decorView.Height;

                if (width <= 0 || height <= 0)
                {
                    decorView.Post(() =>
                    {
                        if (gameplayTouchProtectionActive && decorView.Width > 0 && decorView.Height > 0)
                            applyGestureExclusionRectsApi29(decorView);
                    });
                    return;
                }

                float density = Resources?.DisplayMetrics?.Density ?? 2.75f;
                int edgeWidthPx = Math.Max(1, (int)Math.Round(edge_exclusion_width_dp * density));
                int maxEdgeHeightPx = Math.Min(height, (int)Math.Round(max_edge_exclusion_dp * density));
                int bottomHeightPx = Math.Min(height, (int)Math.Round(bottom_exclusion_height_dp * density));

                var rects = new List<Rect>(3)
                {
                    // Bottom home/task-switcher pill zone (critical for osu!mania bottom keys, catch, and bottom circles).
                    new Rect(0, height - bottomHeightPx, width, height),
                    // Lower-left and lower-right back-gesture zones (up to Android's 200dp per-edge quota).
                    new Rect(0, height - maxEdgeHeightPx, edgeWidthPx, height),
                    new Rect(width - edgeWidthPx, height - maxEdgeHeightPx, width, height),
                };

                decorView.SystemGestureExclusionRects = rects;
            }
            catch
            {
            }
        }

        private void handleIntent(Intent? intent)
        {
            if (intent == null)
                return;

            switch (intent.Action)
            {
                case Intent.ActionDefault:
                    if (intent.Scheme == ContentResolver.SchemeContent)
                    {
                        if (intent.Data != null)
                            handleImportFromUris(intent.Data);
                    }
                    else if (osu_url_schemes.Contains(intent.Scheme))
                    {
                        if (intent.DataString != null)
                            game.HandleLink(intent.DataString);
                    }

                    break;

                case Intent.ActionSend:
                case Intent.ActionSendMultiple:
                {
                    if (intent.ClipData == null)
                        break;

                    var uris = new List<Uri>();

                    for (int i = 0; i < intent.ClipData.ItemCount; i++)
                    {
                        var item = intent.ClipData.GetItemAt(i);
                        if (item?.Uri != null)
                            uris.Add(item.Uri);
                    }

                    handleImportFromUris(uris.ToArray());
                    break;
                }
            }
        }

        private void handleImportFromUris(params Uri[] uris) => Task.Factory.StartNew(async () =>
        {
            var tasks = new List<ImportTask>();

            await Task.WhenAll(uris.Select(async uri =>
            {
                var task = await AndroidImportTask.Create(ContentResolver!, uri).ConfigureAwait(false);

                if (task != null)
                {
                    lock (tasks)
                    {
                        tasks.Add(task);
                    }
                }
            })).ConfigureAwait(false);

            await game.Import(tasks.ToArray()).ConfigureAwait(false);
        }, TaskCreationOptions.LongRunning);

        private sealed class SurfaceFrameRateCallback : Java.Lang.Object, ISurfaceHolderCallback
        {
            private readonly OsuGameActivity activity;

            public SurfaceFrameRateCallback(OsuGameActivity activity)
            {
                this.activity = activity;
            }

            public void SurfaceCreated(ISurfaceHolder holder) => activity.applySurfaceFrameRateHint();

            public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height) => activity.applySurfaceFrameRateHint();

            public void SurfaceDestroyed(ISurfaceHolder holder)
            {
            }
        }
    }
}
