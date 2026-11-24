// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Mods;
using osu.Game.Performance;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class PotatoModeTest
    {
        [TearDown]
        public void TearDown()
        {
            PotatoMode.SetFromConfig(false);
        }

        [Test]
        public void TestConfigDefaultsAreDisabled()
        {
            using (var storage = new TemporaryNativeStorage("osu-config-potato-mode-test"))
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.PotatoMode), Is.False);
                Assert.That(config.Get<bool>(OsuSetting.UltraUltimatePotatoMode), Is.False);
                Assert.That(config.Get<bool>(OsuSetting.UltraPotatoLivePerformance), Is.False);
            }
        }

        [Test]
        public void TestPerfectPerformanceSkippedInUltraUltimatePotatoMode()
        {
            // BeatmapDifficultyCache hands back a bare PerformanceAttributes for the perfect play in ultra ultimate potato mode.
            var skipped = new PerformanceBreakdown(new OsuPerformanceAttributes { Total = 120, Aim = 60 }, new PerformanceAttributes());
            var full = new PerformanceBreakdown(new OsuPerformanceAttributes { Total = 120, Aim = 60 }, new OsuPerformanceAttributes { Total = 300, Aim = 150 });
            var bare = new PerformanceBreakdown(new PerformanceAttributes { Total = 10 }, new PerformanceAttributes { Total = 20 });

            Assert.That(PerformanceBreakdownChart.HasPerfectPerformance(skipped), Is.False);
            Assert.That(PerformanceBreakdownChart.HasPerfectPerformance(full), Is.True);
            Assert.That(PerformanceBreakdownChart.HasPerfectPerformance(bare), Is.True);
        }

        [TestCase(false, false, ExpectedResult = true)]
        [TestCase(false, true, ExpectedResult = true)]
        [TestCase(true, false, ExpectedResult = false)]
        [TestCase(true, true, ExpectedResult = true)]
        public bool TestLivePerformanceInUltraUltimatePotatoMode(bool ultraActive, bool liveInUltra)
            => LivePerformanceTracker.ShouldCalculate(ultraActive, liveInUltra);

        [Test]
        public void TestStandardPotatoModeState()
        {
            PotatoMode.SetFromConfig(true, false);

            Assert.That(PotatoMode.Active, Is.True);
            Assert.That(PotatoMode.UltraActive, Is.False);
        }

        [Test]
        public void TestUltraUltimatePotatoModeImpliesStandardPotatoMode()
        {
            PotatoMode.SetFromConfig(false, true);

            Assert.That(PotatoMode.Active, Is.True);
            Assert.That(PotatoMode.UltraActive, Is.True);
        }

        [Test]
        public void TestBeatmapReadableFromThreadPoolInUltraUltimatePotatoMode()
        {
            PotatoMode.SetFromConfig(false, true);

            // The results screen reads Beatmap from a thread pool task while the load may still be running.
            var working = new SlowTestWorkingBeatmap(new TestBeatmap(new OsuRuleset().RulesetInfo));
            var beatmap = Task.Run(() => working.Beatmap).GetAwaiter().GetResult();

            Assert.That(beatmap, Is.Not.Null);
        }

        [Test]
        public void TestBeatmapAttributesDisplayModsStartEmpty()
        {
            // The mod select footer doesn't set these in the stable grid or in ultra potato mode.
            Assert.That(new BeatmapAttributesDisplay().Mods.Value, Is.Empty);
        }

        private class SlowTestWorkingBeatmap : TestWorkingBeatmap
        {
            public SlowTestWorkingBeatmap(IBeatmap beatmap)
                : base(beatmap)
            {
            }

            protected override IBeatmap GetBeatmap()
            {
                Thread.Sleep(200);
                return base.GetBeatmap();
            }
        }
    }
}
