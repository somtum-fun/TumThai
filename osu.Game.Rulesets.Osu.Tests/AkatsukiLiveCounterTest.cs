// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// osu!somtum: the in-game pp counter for Relax and Autopilot plays, as <c>PerformancePointsCounter</c> works it out:
    /// after each judgement, the hits so far with the difficulty attributes at that judgement's time. A slider is judged
    /// at its head (lazer), before the attributes include it, so the hits can run one ahead of the attributes.
    /// </summary>
    [TestFixture]
    public class AkatsukiLiveCounterTest
    {
        private readonly OsuRuleset ruleset = new OsuRuleset();

        [TestCase(75)]
        [TestCase(218464)]
        [TestCase(1872396)]
        [TestCase(5659599)]
        public void TestRelaxNeverSpikes(int beatmapId) => assertNoSpikes(beatmapId, new OsuModRelax());

        [TestCase(75)]
        [TestCase(218464)]
        [TestCase(1872396)]
        [TestCase(5659599)]
        public void TestAutopilotNeverSpikes(int beatmapId) => assertNoSpikes(beatmapId, new OsuModAutopilot());

        private void assertNoSpikes(int beatmapId, Mod mod)
        {
            var working = getBeatmap(beatmapId);
            Mod[] mods = { mod };
            var timed = ruleset.CreateDifficultyCalculator(working).CalculateTimed(mods);
            var performance = ruleset.CreatePerformanceCalculator()!;
            var objects = working.GetPlayableBeatmap(ruleset.RulesetInfo, mods).HitObjects;

            // Judged in order, a slider at its head, all 300s and a full combo.
            int judged = 0;
            int combo = 0;
            var live = new List<double>();

            foreach (var hitObject in objects)
            {
                judged++;
                combo += hitObject is Slider slider ? slider.NestedHitObjects.Count : 1;
                double judgedAt = hitObject is Slider ? hitObject.StartTime : hitObject.GetEndTime();

                int index = timed.BinarySearch(new TimedDifficultyAttributes(judgedAt, null!));
                if (index < 0)
                    index = ~index - 1;

                var score = new ScoreInfo((BeatmapInfo)working.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Mods = mods,
                    MaxCombo = combo,
                    Accuracy = 1,
                    Statistics = new Dictionary<HitResult, int> { [HitResult.Great] = judged },
                };

                live.Add(performance.Calculate(score, timed[Math.Clamp(index, 0, timed.Count - 1)].Attributes).Total);
            }

            double final = live.Last();
            Assert.That(live.Max(), Is.LessThanOrEqualTo(final * 1.5 + 1), $"peak {live.Max():N0}pp at judgement {live.IndexOf(live.Max()) + 1}, final {final:N0}pp");
        }

        private IWorkingBeatmap getBeatmap(int beatmapId)
        {
            const string assembly_name = "osu.Game.Rulesets.Osu.Tests";

            using (var resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{assembly_name}.Resources.Testing.Beatmaps.akatsuki-rx-{beatmapId}.osu"))
            using (var stream = new LineBufferedReader(resStream!))
            {
                var decoder = Decoder.GetDecoder<Beatmap>(stream);

                return new TestWorkingBeatmap(decoder.Decode(stream))
                {
                    BeatmapInfo =
                    {
                        Ruleset = ruleset.RulesetInfo
                    }
                };
            }
        }
    }
}
