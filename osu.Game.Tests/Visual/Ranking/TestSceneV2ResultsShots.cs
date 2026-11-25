// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Tests.Resources;
using SixLabors.ImageSharp;

namespace osu.Game.Tests.Visual.Ranking
{
    /// <summary>
    /// TumThai: screenshots of the V2 results screen right after a play (intro, grade, results). Run in the visual test
    /// browser with HARNESS_V2_OUT (where the PNGs go); exits when done with HARNESS_EXIT=1. Does nothing without it.
    /// </summary>
    [Explicit("Screenshot harness: needs a window and HARNESS_V2_OUT.")]
    public partial class TestSceneV2ResultsShots : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private System.Threading.Tasks.Task<Image<SixLabors.ImageSharp.PixelFormats.Rgba32>>? pending;
        private double shownAt;

        public TestSceneV2ResultsShots()
        {
            string? outDir = Environment.GetEnvironmentVariable("HARNESS_V2_OUT");
            if (string.IsNullOrEmpty(outDir))
                return;

            Directory.CreateDirectory(outDir);

            AddStep("use V2 results", () => config.SetValue(OsuSetting.ResultsScreenStyle, ResultsScreenStyle.V2));
            AddStep("show results", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                score.Rank = ScoreRank.X;
                score.Accuracy = 1;
                score.MaxCombo = score.GetMaximumAchievableCombo();
                score.Statistics[HitResult.Miss] = 0;
                Child = new ResultsHost(new ShotResultsScreen(score));
                shownAt = Time.Current;
            });

            shot(outDir, "1-full-combo", 900);
            shot(outDir, "2-grade", 1900);
            shot(outDir, "3-results", 4500);

            if (Environment.GetEnvironmentVariable("HARNESS_EXIT") == "1")
                AddStep("exit", () => host.Exit());
        }

        private void shot(string outDir, string name, double at)
        {
            AddUntilStep($"wait for {name}", () => Time.Current - shownAt >= at);
            AddStep($"shot {name}", () => pending = host.TakeScreenshotAsync());
            AddUntilStep("saved", () =>
            {
                if (pending?.IsCompleted != true)
                    return false;

                pending.GetResultSafely().SaveAsPng(Path.Combine(outDir, $"{name}.png"));
                pending = null;
                return true;
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The test browser stops after the first step unless "Run all steps" is ticked: run them all here.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HARNESS_V2_OUT")))
                Scheduler.AddDelayed(() => RunAllSteps(), 2000);
        }

        private partial class ResultsHost : Container
        {
            // A player having been there is what makes the results flair (and the V2 intro) play.
            [Cached(typeof(Player))]
            public readonly Player Player = new TestPlayer();

            public ResultsHost(ResultsScreen screen)
            {
                RelativeSizeAxes = Axes.Both;
                OsuScreenStack stack;
                InternalChild = stack = new OsuScreenStack { RelativeSizeAxes = Axes.Both };
                stack.Push(screen);
            }
        }

        private partial class ShotResultsScreen : SoloResultsScreen
        {
            public ShotResultsScreen(ScoreInfo score)
                : base(score)
            {
                AllowRetry = true;
                IsLocalPlay = true;
            }
        }
    }
}
