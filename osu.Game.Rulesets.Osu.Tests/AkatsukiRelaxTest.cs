// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: Akatsuki's Relax pp (akatsuki-pp-rs osu_2019 @ 591de0d), ported 1:1.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Beatmaps.Legacy;
using osu.Game.IO;
using osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Relax;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// Expected values come from the live server's pp sidecar (akatsuki-pp-rs 591de0d, <c>osu_2019::OsuPP</c>),
    /// called with the same legacy mods and hit counts. Partial (failed) plays use <c>passed_objects</c> = sum of the hit counts.
    /// </summary>
    [TestFixture]
    public class AkatsukiRelaxTest
    {
        private const double star_tolerance = 0.0001;
        private const double pp_tolerance = 0.001;

        private readonly OsuRuleset ruleset = new OsuRuleset();

        [TestCase(75, 128, 194, 0, 0, 0, 314, true, 2.4089033603668213, 30.78862190246582)] // RX, v3 map (24ms offset)
        [TestCase(75, 136, 180, 10, 2, 2, 120, true, 2.4089033603668213, 10.274628639221191)] // RXHD
        [TestCase(75, 384, 190, 4, 0, 0, 314, true, 1.9296796321868896, 9.090457916259766)] // RXHT
        [TestCase(218464, 144, 728, 0, 0, 0, 1182, true, 4.555177688598633, 236.58367919921875)] // RXHR
        [TestCase(218464, 200, 690, 30, 5, 3, 600, true, 5.810483932495117, 113.56060028076172)] // RXHDDT
        [TestCase(218464, 130, 710, 15, 3, 0, 1182, true, 3.9648828506469727, 36.21989822387695)] // RXEZ
        [TestCase(218464, 128, 360, 3, 1, 0, 250, false, 4.112096786499023, 63.38385772705078)] // RX, failed at 364 objects
        [TestCase(1872396, 192, 1256, 0, 0, 0, 1682, true, 8.78883171081543, 963.1858520507812)] // RXDT
        [TestCase(1872396, 1152, 1200, 40, 10, 6, 900, true, 6.144179344177246, 321.9720764160156)] // RXFL
        [TestCase(1872396, 192, 600, 20, 5, 2, 300, false, 8.058967590332031, 409.29620361328125)] // RXDT, failed at 627 objects
        [TestCase(5659599, 128, 186, 0, 0, 0, 278, true, 5.78807258605957, 211.01914978027344)] // RX, CS2.4 (small circle bonus)
        [TestCase(5659599, 200, 170, 12, 2, 2, 140, true, 8.026473999023438, 319.41925048828125)] // RXHDDT
        [TestCase(5659599, 1160, 180, 6, 0, 0, 278, true, 5.78807258605957, 222.72311401367188)] // RXHDFL
        public void TestAgainstReference(int beatmapId, int legacyMods, int n300, int n100, int n50, int misses, int combo, bool passed,
                                         double expectedStars, double expectedPp)
        {
            var working = getBeatmap(beatmapId);
            var mods = ruleset.ConvertFromLegacyMods((LegacyMods)legacyMods).ToArray();
            // Through the ruleset, as the game does: it hands Relax plays to Akatsuki's calculators.
            var calculator = ruleset.CreateDifficultyCalculator(working);

            AkatsukiRelaxDifficultyAttributes attributes;

            if (passed)
                attributes = (AkatsukiRelaxDifficultyAttributes)calculator.Calculate(mods);
            else
            {
                // The in-game pp counter uses timed attributes: the attributes after N objects match `passed_objects = N`.
                int passedObjects = n300 + n100 + n50 + misses;
                attributes = (AkatsukiRelaxDifficultyAttributes)calculator.CalculateTimed(mods)[passedObjects - 1].Attributes;
                Assert.That(attributes.ObjectCount, Is.EqualTo(passedObjects));
            }

            var score = new ScoreInfo((BeatmapInfo)working.BeatmapInfo, ruleset.RulesetInfo)
            {
                Mods = mods,
                MaxCombo = combo,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Great] = n300,
                    [HitResult.Ok] = n100,
                    [HitResult.Meh] = n50,
                    [HitResult.Miss] = misses,
                }
            };

            var performance = ruleset.CreatePerformanceCalculator()!.Calculate(score, attributes);

            Assert.That(attributes.StarRating, Is.EqualTo(expectedStars).Within(star_tolerance));
            Assert.That(performance.Total, Is.EqualTo(expectedPp).Within(pp_tolerance));
        }

        [TestCase(75, 128, 314)]
        [TestCase(218464, 128, 1182)]
        [TestCase(1872396, 192, 1682)]
        [TestCase(5659599, 144, 278)]
        public void TestTimedEndsOnFullAttributes(int beatmapId, int legacyMods, int expectedMaxCombo)
        {
            var working = getBeatmap(beatmapId);
            var mods = ruleset.ConvertFromLegacyMods((LegacyMods)legacyMods).ToArray();
            var calculator = new AkatsukiRelaxDifficultyCalculator(ruleset.RulesetInfo, working);

            var attributes = calculator.Calculate(mods);
            var timed = calculator.CalculateTimed(mods);

            Assert.That(attributes.MaxCombo, Is.EqualTo(expectedMaxCombo));
            Assert.That(timed.Last().Attributes, Is.EqualTo(attributes).UsingPropertiesComparer());
        }

        private IWorkingBeatmap getBeatmap(int beatmapId)
        {
            const string assembly_name = "osu.Game.Rulesets.Osu.Tests";

            // Offsets are left on (as in the game), so v3/v4 maps get lazer's 24ms offset which the calculator undoes.
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
