// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Select;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>
    /// TumThai: screenshots of the osu!stable theme's ways to notifications and osu!direct without a keyboard, in the whole
    /// game: the main menu's tabs with a download going on, each tab tapped, and song select's
    /// <see cref="StableNotificationIndicator"/> before and after a tap. Run in the visual test browser with HARNESS_OUT (HARNESS_UISCALE optional); exits when done when HARNESS_EXIT=1.
    /// </summary>
    [Explicit("Screenshot harness: needs a window and HARNESS_OUT.")]
    public partial class TestSceneStableNotificationsShots : OsuGameTestScene
    {
        private Task<Image<Rgba32>>? pending;

        [Test]
        public void TestShots()
        {
            string outDir = Environment.GetEnvironmentVariable("HARNESS_OUT") ?? Path.GetTempPath();


            if (float.TryParse(Environment.GetEnvironmentVariable("HARNESS_UISCALE"), NumberStyles.Float, CultureInfo.InvariantCulture, out float uiScale))
                AddStep($"ui scale {uiScale}", () => config.SetValue(OsuSetting.UIScale, uiScale));

            ConfirmAtMainMenu();

            // The game loads its overlays in the background for a while after the main menu shows; changing the theme while
            // one is still loading trips it up, so give them time first.
            AddWaitStep("overlays load", 40);
            AddStep("stable theme", () => config.SetValue(OsuSetting.InterfaceTheme, InterfaceTheme.OsuStable));
            AddWaitStep("settle", 10);
            AddStep("dismiss start-up dialogs", () => Game.ChildrenOfType<DialogOverlay>().Single().CurrentDialog?.PerformOkAction());
            AddWaitStep("settle", 5);

            AddStep("a download going on", () =>
            {
                Game.Notifications.Post(new SimpleNotification { Text = "Something happened while you were away." });
                Game.Notifications.Post(new ProgressNotification
                {
                    Text = "Downloading Some Artist - Some Song",
                    Progress = 0.4f,
                    State = ProgressNotificationState.Active,
                });
            });
            AddWaitStep("toasts go", 30);
            shot(outDir, "menu");

            AddStep("tap Notifications tab", () => tap(label("Notifications")));
            AddUntilStep("notifications open", () => Game.Notifications.State.Value == Visibility.Visible);
            AddWaitStep("settle", 5);
            shot(outDir, "menu-notifications");

            AddStep("close notifications", () => Game.Notifications.Hide());
            AddWaitStep("settle", 5);
            AddStep("tap osu!direct tab", () => tap(label("osu!direct")));
            AddWaitStep("settle", 15);
            shot(outDir, "menu-direct");
            AddStep("close osu!direct", () => Game.CloseAllOverlays(false));

            PushAndConfirm(() => new SoloSongSelect());
            AddWaitStep("settle", 15);
            shot(outDir, "songselect");

            AddStep("tap the bell", () => tap(Game.ChildrenOfType<StableNotificationIndicator>().Single()));
            AddUntilStep("notifications open", () => Game.Notifications.State.Value == Visibility.Visible);
            AddWaitStep("settle", 5);
            shot(outDir, "songselect-notifications");

            // The toolbar's hotkeys work without the toolbar.
            AddStep("close notifications", () => Game.Notifications.Hide());
            AddUntilStep("closed", () => Game.Notifications.State.Value == Visibility.Hidden);
            AddStep("press ctrl-n", () =>
            {
                InputManager.PressKey(osuTK.Input.Key.ControlLeft);
                InputManager.Key(osuTK.Input.Key.N);
                InputManager.ReleaseKey(osuTK.Input.Key.ControlLeft);
            });
            AddUntilStep("ctrl-n opens notifications", () => Game.Notifications.State.Value == Visibility.Visible);
            AddStep("close notifications", () => Game.Notifications.Hide());

            // The mode menu fits the screen's height (it went off the top on wide phones).
            AddStep("open the mode menu", () => typeof(StableSongSelectFooter).GetMethod("toggleModeMenu", BindingFlags.Instance | BindingFlags.NonPublic)!
                                                                               .Invoke(Game.ChildrenOfType<StableSongSelectFooter>().Single(), null));
            AddWaitStep("settle", 5);
            AddAssert("every mode row is on screen", () =>
            {
                var screen = Game.ScreenSpaceDrawQuad.AABBFloat;
                return Game.ChildrenOfType<StableSongSelectFooter>().Single().ChildrenOfType<OsuSpriteText>()
                           .Where(t => t.Text.ToString() is "osu!" or "osu!taiko" or "osu!catch" or "osu!mania" or "More modes")
                           .All(t => t.ScreenSpaceDrawQuad.AABBFloat.Top >= screen.Top - 1);
            });
            shot(outDir, "songselect-modes");

            if (Environment.GetEnvironmentVariable("HARNESS_EXIT") == "1")
                AddStep("exit", () => host.Exit());
        }
        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The test browser stops after the first step unless "Run all steps" is ticked: run them all here.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HARNESS_OUT")))
                Scheduler.AddDelayed(() => RunAllSteps(), 2000);
        }

        private OsuConfigManager config => Game.Dependencies.Get<OsuConfigManager>();

        private GameHost host => Game.Dependencies.Get<GameHost>();

        // A main menu bottom tab, by its label.
        private Drawable label(string startsWith) => Game.ChildrenOfType<OsuSpriteText>().First(t => t.IsPresent && t.Text.ToString().StartsWith(startsWith, StringComparison.Ordinal));

        private void tap(Drawable target)
        {
            InputManager.MoveMouseTo(target);
            InputManager.Click(osuTK.Input.MouseButton.Left);
        }

        private void shot(string outDir, string name)
        {
            AddStep($"shot {name}", () => pending = host.TakeScreenshotAsync());
            AddUntilStep("saved", () =>
            {
                if (pending?.IsCompleted != true)
                    return false;

                using (var image = pending.GetResultSafely())
                {
                    var quad = Game.ScreenSpaceDrawQuad.AABBFloat;
                    var crop = new Rectangle((int)quad.X, (int)quad.Y, (int)quad.Width, (int)quad.Height);
                    crop.Intersect(new Rectangle(0, 0, image.Width, image.Height));
                    image.Mutate(i => i.Crop(crop));
                    image.SaveAsPng(Path.Combine(outDir, $"{name}.png"));
                }

                pending = null;
                return true;
            });
        }
    }
}
