// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Tests.Visual.Ranking
{
    /// <summary>
    /// TumThai: screenshots of the osu!stable theme's results screen with each skin in a folder, for checking layouts
    /// against many skins at once. Run in the visual test browser with HARNESS_SKINS (a folder of .osk files) and
    /// HARNESS_OUT (where the PNGs go); it exits when done when HARNESS_EXIT=1. Does nothing without HARNESS_SKINS.
    /// </summary>
    [Explicit("Screenshot harness: needs a window and HARNESS_SKINS.")]
    public partial class TestSceneStableSkinShots : ScreenTestScene
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        public TestSceneStableSkinShots()
        {
            string? skinDir = Environment.GetEnvironmentVariable("HARNESS_SKINS");
            string outDir = Environment.GetEnvironmentVariable("HARNESS_OUT") ?? Path.GetTempPath();

            if (string.IsNullOrEmpty(skinDir) || !Directory.Exists(skinDir))
                return;

            AddStep("stable theme", () => config.SetValue(OsuSetting.InterfaceTheme, InterfaceTheme.OsuStable));

            // HARNESS_UISCALE: the phone's UI scale (Settings > Graphics), e.g. 1.3.
            if (float.TryParse(Environment.GetEnvironmentVariable("HARNESS_UISCALE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float uiScale))
                AddStep($"ui scale {uiScale}", () => config.SetValue(OsuSetting.UIScale, uiScale));

            foreach (string osk in Directory.GetFiles(skinDir, "*.osk").OrderBy(f => f, StringComparer.Ordinal))
            {
                string name = Path.GetFileNameWithoutExtension(osk);

                AddStep($"import {name}", () => importing = skins.Import(new ImportTask(osk)));
                AddUntilStep($"skin {name}", () =>
                {
                    if (importing?.IsCompleted != true)
                        return false;

                    var imported = importing.GetResultSafely();
                    if (imported != null)
                        skins.CurrentSkinInfo.Value = imported;
                    importing = null;
                    return true;
                });
                AddStep("results", () => LoadScreen(new SoloResultsScreen(TestResources.CreateTestScoreInfo())));
                AddWaitStep("settle", 25);
                string shotPath = Path.Combine(outDir, $"{sanitise(name)}-results.png");
                // A quick circle with the mouse just before the shot, so the menu cursor and its trail are in it.
                AddStep("rest in corner", () => InputManager.MoveMouseTo(Stack.ScreenSpaceDrawQuad.BottomRight - new osuTK.Vector2(20)));
                AddWaitStep("rest", 3);
                // a tap where the sweep starts, as a finger landing there (the cursor jumps from the corner)
                AddStep("tap", () =>
                {
                    var area = Stack.ScreenSpaceDrawQuad.AABBFloat;
                    var touch = new osu.Framework.Input.Touch(osu.Framework.Input.TouchSource.Touch1, new osuTK.Vector2(area.Left + area.Width * 0.1f, area.Centre.Y));
                    InputManager.BeginTouch(touch);
                    InputManager.EndTouch(touch);
                });
                AddStep("sweep start", () => sweepStart = null);
                // The shot is taken as the sweep ends, while the trail is still showing.
                AddUntilStep("sweep", sweep);
                AddUntilStep("saved", () =>
                {
                    if (pending?.IsCompleted != true)
                        return false;

                    save(pending.GetResultSafely(), shotPath);
                    pending = null;
                    return true;
                });
                // Page 2 (Online Ranking): only shown for a submitted score, so it's opened by hand here.
                AddStep("online ranking", showOnlineRanking);
                AddWaitStep("settle", 15);
                string rankingPath = Path.Combine(outDir, $"{sanitise(name)}-ranking.png");
                AddStep($"shot {name} ranking", () => pending = host.TakeScreenshotAsync());
                AddUntilStep("saved", () =>
                {
                    if (pending?.IsCompleted != true)
                        return false;

                    save(pending.GetResultSafely(), rankingPath);
                    pending = null;
                    return true;
                });
                AddStep("back", () => Stack.Exit());
                AddWaitStep("settle", 5);

                // HARNESS_GAMEPLAY=1: also a few seconds into an autoplay, for the HUD.
                if (Environment.GetEnvironmentVariable("HARNESS_GAMEPLAY") == "1")
                {
                    AddStep("play", () =>
                    {
                        Ruleset.Value = new OsuRuleset().RulesetInfo;
                        Beatmap.Value = CreateWorkingBeatmap(Ruleset.Value);
                        SelectedMods.Value = new Mod[] { new OsuModAutoplay() };
                        playStart = null;
                        LoadScreen(new TestPlayer(false, false));
                    });
                    AddUntilStep("playing", () =>
                    {
                        if (Stack.CurrentScreen is not TestPlayer player || !player.IsLoaded || player.GameplayClockContainer.CurrentTime < 0)
                            return false;

                        playStart ??= Time.Current;
                        return Time.Current - playStart > 6000;
                    });
                    string playPath = Path.Combine(outDir, $"{sanitise(name)}-play.png");
                    AddStep($"shot {name} play", () => pending = host.TakeScreenshotAsync());
                    AddUntilStep("saved", () =>
                    {
                        if (pending?.IsCompleted != true)
                            return false;

                        save(pending.GetResultSafely(), playPath);
                        pending = null;
                        return true;
                    });
                    AddStep("leave", () =>
                    {
                        Stack.Exit();
                        SelectedMods.Value = Array.Empty<Mod>();
                    });
                    AddWaitStep("settle", 5);
                }
            }

            if (Environment.GetEnvironmentVariable("HARNESS_EXIT") == "1")
                AddStep("exit", () => host.Exit());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The test browser stops after the first step unless "Run all steps" is ticked: run them all here.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HARNESS_SKINS")))
                Scheduler.AddDelayed(() => RunAllSteps(), 2000);
        }

        private void showOnlineRanking()
        {
            var content = Stack.ChildrenOfType<StableResultsContent>().FirstOrDefault();
            if (content == null)
                return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var score = content.SelectedScore.Value ?? TestResources.CreateTestScoreInfo();
            typeof(StableResultsContent).GetField("<SubmittedScore>k__BackingField", flags)!.SetValue(content, score);
            var dialog = (StableRankingDialog)typeof(StableResultsContent).GetField("rankingDialog", flags)!.GetValue(content)!;
            dialog.SetScore(score);
            typeof(StableResultsContent).GetMethod("showRankingDialog", flags)!.Invoke(content, new object[] { true });
        }

        private System.Threading.Tasks.Task<Image<SixLabors.ImageSharp.PixelFormats.Rgba32>>? pending;
        private double? playStart;
        private double? sweepStart;

        private bool sweep()
        {
            const double duration = 400;

            sweepStart ??= Time.Current;
            double progress = Math.Min(1, (Time.Current - sweepStart.Value) / duration);

            // A horizontal line across the middle, from the left; the mouse rested in the bottom-right corner before it, so a
            // diagonal streak from that corner in the shot means the trail joined the jump.
            var area = Stack.ScreenSpaceDrawQuad.AABBFloat;
            // (a wave, as a hand moves: the input resampler only passes on points where the direction changes)
            InputManager.MoveMouseTo(new osuTK.Vector2(area.Left + area.Width * (0.1f + 0.6f * (float)progress),
                area.Centre.Y + area.Height * 0.08f * (float)Math.Sin(progress * Math.PI * 4)));

            if (progress < 1)
                return false;

            pending = host.TakeScreenshotAsync();
            return true;
        }
        private System.Threading.Tasks.Task<Live<SkinInfo>?>? importing;

        private void save(Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image, string path)
        {
            // Only the game screen: the test browser's own panels are cropped off.
            var quad = Stack.ScreenSpaceDrawQuad.AABBFloat;
            var crop = new Rectangle((int)quad.X, (int)quad.Y, (int)quad.Width, (int)quad.Height);
            crop.Intersect(new Rectangle(0, 0, image.Width, image.Height));
            image.Mutate(i => i.Crop(crop));
            image.SaveAsPng(path);
            image.Dispose();
        }

        private static string sanitise(string name) => string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_')).Trim('_');
    }
}
