// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Akatsuki;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// osu!somtum: checks the Akatsuki Autopilot calculators against the values the live server's pp sidecar
    /// (akatsuki-pp-rs 591de0d) gave for the same plays.
    /// </summary>
    [TestFixture]
    public class AkatsukiAutopilotTest : DifficultyCalculatorTest
    {
        protected override string ResourceAssembly => "osu.Game.Rulesets.Osu.Tests";

        private const double star_tolerance = 0.0001;
        private const double pp_relative_tolerance = 0.0001;

        // Map, legacy mod bits (always with Autopilot = 8192), lazer, 300s, 100s, 50s, misses, combo,
        // large tick hits and slider end hits (lazer only, -1 otherwise), then the sidecar's star rating and pp.
        // lazer = false is a stable play, which here is a play with the Classic mod.
        [TestCase("75", 8192, false, 194, 0, 0, 0, 314, -1, -1, 1.665615788639, 25.948335081141536)]
        [TestCase("75", 8200, false, 194, 0, 0, 0, 314, -1, -1, 1.665615788639, 28.488704114814855)]
        [TestCase("75", 8256, false, 194, 0, 0, 0, 314, -1, -1, 2.3476941530082995, 73.36631503357319)]
        [TestCase("75", 8208, false, 179, 10, 2, 3, 157, -1, -1, 1.665615788639, 12.405912862389334)]
        [TestCase("75", 9224, false, 189, 5, 0, 0, 314, -1, -1, 1.8028607474797054, 19.104363030896835)]
        [TestCase("75", 8194, false, 191, 3, 0, 0, 307, -1, -1, 1.665615788639, 7.137870390830446)]
        [TestCase("75", 8448, false, 169, 20, 5, 0, 284, -1, -1, 1.3125330703724254, 0.8391483286595784)]
        [TestCase("75", 8192, true, 184, 8, 0, 2, 188, 87, 26, 1.665615788639, 11.130235481036035)]
        [TestCase("75", 8264, true, 194, 0, 0, 0, 314, 90, 30, 2.3476941530082995, 83.51446871390597)]
        [TestCase("75", 9216, true, 188, 4, 1, 1, 274, 89, 28, 1.7661288294739836, 16.394260658846626)]
        [TestCase("1639738", 8192, false, 148, 0, 0, 0, 148, -1, -1, 2.7398400627887063, 62.53056640443636)]
        [TestCase("1639738", 8200, false, 148, 0, 0, 0, 148, -1, -1, 2.7398400627887063, 68.45729153908096)]
        [TestCase("1639738", 8256, false, 148, 0, 0, 0, 148, -1, -1, 3.8842514038189195, 149.86155527907704)]
        [TestCase("1639738", 8208, false, 133, 10, 2, 3, 74, -1, -1, 2.7398400627887063, 26.91243495174981)]
        [TestCase("1639738", 9224, false, 143, 5, 0, 0, 148, -1, -1, 2.9588075275824184, 48.902799300297666)]
        [TestCase("1639738", 8194, false, 145, 3, 0, 0, 141, -1, -1, 2.738711570453507, 20.3284891837602)]
        [TestCase("1639738", 8448, false, 123, 20, 5, 0, 118, -1, -1, 2.19788327741812, 4.1283315853876585)]
        [TestCase("1639738", 8192, true, 138, 8, 0, 2, 88, 0, 0, 2.7398400627887063, 23.936826068597867)]
        [TestCase("1639738", 8264, true, 148, 0, 0, 0, 148, 0, 0, 3.8842514038189195, 161.85047970140317)]
        [TestCase("1639738", 9216, true, 142, 4, 1, 1, 108, 0, 0, 2.889261423664232, 36.1365128696575)]
        [TestCase("3451428", 8192, false, 611, 0, 0, 0, 900, -1, -1, 4.608278351345347, 188.12993809069434)]
        [TestCase("3451428", 8200, false, 611, 0, 0, 0, 900, -1, -1, 4.608278351345347, 206.1159349573773)]
        [TestCase("3451428", 8256, false, 611, 0, 0, 0, 900, -1, -1, 7.088509910359416, 540.2452088321639)]
        [TestCase("3451428", 8208, false, 596, 10, 2, 3, 450, -1, -1, 4.608278351345347, 157.9333354603805)]
        [TestCase("3451428", 9224, false, 606, 5, 0, 0, 900, -1, -1, 4.8295863880834835, 201.00572151368576)]
        [TestCase("3451428", 8194, false, 608, 3, 0, 0, 893, -1, -1, 4.398934213214673, 87.81309277931888)]
        [TestCase("3451428", 8448, false, 586, 20, 5, 0, 870, -1, -1, 3.5810987727849186, 50.204544511865436)]
        [TestCase("3451428", 8192, true, 601, 8, 0, 2, 540, 118, 164, 4.608278351345347, 154.52821748181472)]
        [TestCase("3451428", 8264, true, 611, 0, 0, 0, 900, 121, 168, 7.088509910359416, 595.7972144677292)]
        [TestCase("3451428", 9216, true, 605, 4, 1, 1, 860, 120, 166, 4.777417111510397, 180.11562207254124)]
        [TestCase("5542525", 8192, false, 476, 0, 0, 0, 690, -1, -1, 3.025276807555514, 83.59090115299934)]
        [TestCase("5542525", 8200, false, 476, 0, 0, 0, 690, -1, -1, 3.025276807555514, 91.18505817601411)]
        [TestCase("5542525", 8256, false, 476, 0, 0, 0, 690, -1, -1, 4.298850268146888, 198.83854030241372)]
        [TestCase("5542525", 8208, false, 461, 10, 2, 3, 345, -1, -1, 3.025276807555514, 64.55524513258096)]
        [TestCase("5542525", 9224, false, 471, 5, 0, 0, 690, -1, -1, 3.4035129006808393, 82.5224065398082)]
        [TestCase("5542525", 8194, false, 473, 3, 0, 0, 683, -1, -1, 2.844334927255289, 26.610235172748904)]
        [TestCase("5542525", 8448, false, 451, 20, 5, 0, 660, -1, -1, 2.4091633443676264, 10.987058245509017)]
        [TestCase("5542525", 8192, true, 466, 8, 0, 2, 414, 1, 206, 3.025276807555514, 65.92882701908088)]
        [TestCase("5542525", 8264, true, 476, 0, 0, 0, 690, 4, 210, 4.298850268146888, 240.21030430722465)]
        [TestCase("5542525", 9216, true, 470, 4, 1, 1, 650, 3, 208, 3.3195816663641153, 83.57281491534657)]
        [TestCase("218464", 8192, false, 728, 0, 0, 0, 1182, -1, -1, 3.3560575136705184, 79.42457309048875)]
        [TestCase("218464", 8200, false, 728, 0, 0, 0, 1182, -1, -1, 3.3560575136705184, 87.11828324036766)]
        [TestCase("218464", 8256, false, 728, 0, 0, 0, 1182, -1, -1, 4.620649563916, 205.42445212137073)]
        [TestCase("218464", 8208, false, 713, 10, 2, 3, 591, -1, -1, 3.3560575136705184, 105.27437247951771)]
        [TestCase("218464", 9224, false, 723, 5, 0, 0, 1182, -1, -1, 3.6778856032566924, 87.1319534568324)]
        [TestCase("218464", 8194, false, 725, 3, 0, 0, 1175, -1, -1, 3.1798884334931, 35.55524720596926)]
        [TestCase("218464", 8448, false, 703, 20, 5, 0, 1152, -1, -1, 2.668199203244866, 15.675268022424476)]
        [TestCase("218464", 8192, true, 718, 8, 0, 2, 709, 175, 272, 3.3560575136705184, 65.59250444913033)]
        [TestCase("218464", 8264, true, 728, 0, 0, 0, 1182, 178, 276, 4.620649563916, 239.2021605721146)]
        [TestCase("218464", 9216, true, 722, 4, 1, 1, 1142, 177, 274, 3.5960323292858174, 80.52670987042433)]
        public void TestAgainstSidecar(string name, int legacyMods, bool lazer, int countGreat, int countOk, int countMeh, int countMiss, int combo,
                                       int largeTickHits, int sliderEndHits, double expectedStarRating, double expectedPerformance)
        {
            var mods = createMods(legacyMods, lazer);
            var workingBeatmap = GetBeatmap(name);
            var calculator = CreateDifficultyCalculator(workingBeatmap);

            var attributes = (AkatsukiAutopilotDifficultyAttributes)calculator.Calculate(mods);
            Assert.That(attributes.StarRating, Is.EqualTo(expectedStarRating).Within(star_tolerance), "star rating");

            // The in-game counter works off timed attributes; the last one must be the full map's.
            var timedAttributes = calculator.CalculateTimed(mods);
            Assert.That(timedAttributes.Last().Attributes.StarRating, Is.EqualTo(attributes.StarRating).Within(CHECK_PRECISION), "timed star rating");

            var statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = countGreat,
                [HitResult.Ok] = countOk,
                [HitResult.Meh] = countMeh,
                [HitResult.Miss] = countMiss,
            };

            double accuracy;

            if (lazer)
            {
                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, mods);
                int largeTicks = playableBeatmap.HitObjects.OfType<Slider>().Sum(s => s.NestedHitObjects.Count(n => n is SliderTick || n is SliderRepeat));
                int sliders = attributes.SliderCount;

                statistics[HitResult.LargeTickHit] = largeTickHits;
                statistics[HitResult.LargeTickMiss] = largeTicks - largeTickHits;
                statistics[HitResult.SliderTailHit] = sliderEndHits;

                accuracy = (double)(300 * countGreat + 100 * countOk + 50 * countMeh + 150 * sliderEndHits + 30 * largeTickHits)
                           / (300 * (countGreat + countOk + countMeh + countMiss) + 150 * sliders + 30 * largeTicks);
            }
            else
                accuracy = (double)(300 * countGreat + 100 * countOk + 50 * countMeh) / (300 * (countGreat + countOk + countMeh + countMiss));

            var score = new ScoreInfo(workingBeatmap.Beatmap.BeatmapInfo, new OsuRuleset().RulesetInfo)
            {
                Mods = mods,
                Accuracy = accuracy,
                MaxCombo = combo,
                Statistics = statistics,
            };

            var performance = (OsuPerformanceAttributes)new AkatsukiAutopilotPerformanceCalculator().Calculate(score, attributes);

            Assert.That(performance.Aim, Is.Zero, "aim pp");
            Assert.That(performance.Total, Is.EqualTo(expectedPerformance).Within(Math.Max(expectedPerformance * pp_relative_tolerance, 0.001)), "pp");
        }

        [Test]
        public void TestAimCountsWithoutAutopilot()
        {
            var attributes = (AkatsukiAutopilotDifficultyAttributes)CreateDifficultyCalculator(GetBeatmap("3451428")).Calculate();
            Assert.That(attributes.AimDifficulty, Is.GreaterThan(0));
        }

        private static Mod[] createMods(int legacyMods, bool lazer)
        {
            var mods = new List<Mod> { new OsuModAutopilot() };

            if ((legacyMods & 2) != 0) mods.Add(new OsuModEasy());
            if ((legacyMods & 8) != 0) mods.Add(new OsuModHidden());
            if ((legacyMods & 16) != 0) mods.Add(new OsuModHardRock());
            if ((legacyMods & 64) != 0) mods.Add(new OsuModDoubleTime());
            if ((legacyMods & 256) != 0) mods.Add(new OsuModHalfTime());
            if ((legacyMods & 1024) != 0) mods.Add(new OsuModFlashlight());

            // A stable play counts like a lazer play with Classic (no slider head accuracy).
            if (!lazer)
                mods.Add(new OsuModClassic());

            return mods.ToArray();
        }

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new AkatsukiAutopilotDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new OsuRuleset();
    }
}
