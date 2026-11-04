// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Input;
using osu.Framework.Screens;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Osu.Skinning.Argon;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;
using osu.Game.Utils;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    public partial class TestSceneOsuModDroid : RateAdjustedBeatmapTestScene
    {
        private ScoreAccessibleSoloPlayer currentPlayer = null!;
        private readonly ManualClock manualClock = new ManualClock { Rate = 1 };

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, new FramedClock(manualClock), Audio);

        public override void SetUpSteps()
        {
            AddStep("release all touches", () =>
            {
                foreach (TouchSource source in InputManager.CurrentState.Touch.ActiveSources.ToArray())
                    InputManager.EndTouch(new Touch(source, Vector2.Zero));
            });

            base.SetUpSteps();
        }

        [Test]
        public void TestExactDroidCircleScaleStackingHitWindowsAndSpinners()
        {
            IBeatmap standardBeatmap = null!;
            IBeatmap droidBeatmap = null!;
            IBeatmap preciseDroidBeatmap = null!;
            IBeatmap droidHardRockA = null!;
            IBeatmap droidHardRockB = null!;
            IBeatmap droidDiffAdjust = null!;

            AddStep("create playable beatmaps", () =>
            {
                OsuBeatmap createBeatmap() => new OsuBeatmap
                {
                    BeatmapVersion = 14,
                    StackLeniency = 0.7f,
                    BeatmapInfo =
                    {
                        Difficulty = new BeatmapDifficulty
                        {
                            CircleSize = 4f,
                            OverallDifficulty = 8f,
                            ApproachRate = 9f,
                            DrainRate = 5f,
                        },
                        Ruleset = new OsuRuleset().RulesetInfo,
                    },
                    HitObjects =
                    {
                        new HitCircle { StartTime = 1000, Position = new Vector2(256, 192) },
                        new HitCircle { StartTime = 1100, Position = new Vector2(256, 192) },
                        new Slider
                        {
                            StartTime = 2000,
                            RepeatCount = 1,
                            Position = new Vector2(160, 192),
                            Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(150, 0) }),
                        },
                        new Spinner
                        {
                            StartTime = 4000,
                            Duration = 2500,
                            Position = OsuPlayfield.BASE_SIZE / 2,
                        },
                    },
                };

                var rulesetInfo = new OsuRuleset().RulesetInfo;
                standardBeatmap = CreateWorkingBeatmap(createBeatmap()).GetPlayableBeatmap(rulesetInfo, Array.Empty<Mod>());
                droidBeatmap = CreateWorkingBeatmap(createBeatmap()).GetPlayableBeatmap(rulesetInfo, new Mod[] { new OsuModDroid() });
                preciseDroidBeatmap = CreateWorkingBeatmap(createBeatmap()).GetPlayableBeatmap(rulesetInfo, new Mod[] { new OsuModDroid { PreciseTiming = { Value = true } } });
                droidHardRockA = CreateWorkingBeatmap(createBeatmap()).GetPlayableBeatmap(rulesetInfo, new Mod[] { new OsuModDroid(), new OsuModHardRock() });
                droidHardRockB = CreateWorkingBeatmap(createBeatmap()).GetPlayableBeatmap(rulesetInfo, new Mod[] { new OsuModHardRock(), new OsuModDroid() });
                droidDiffAdjust = CreateWorkingBeatmap(createBeatmap()).GetPlayableBeatmap(rulesetInfo, new Mod[]
                {
                    new OsuModDifficultyAdjust { CircleSize = { Value = 6.5f } },
                    new OsuModDroid(),
                });
            });

            AddAssert("exact osu!droid circle scale and stack offset applied", () =>
            {
                float expectedScale = OsuModDroid.CalculateDroidScale(4f);
                var stdCircle = (HitCircle)standardBeatmap.HitObjects[0];
                var drCircle = (HitCircle)droidBeatmap.HitObjects[0];

                return Precision.AlmostEquals(expectedScale, 1.05032f, 1e-4f)
                       && Precision.AlmostEquals(drCircle.Scale, expectedScale)
                       && Precision.AlmostEquals((float)drCircle.Radius, 67.22f, 0.05f)
                       && drCircle.Radius > stdCircle.Radius * 1.8
                       && drCircle.StackHeight == 1
                       && Precision.AlmostEquals(drCircle.StackOffset, new Vector2(expectedScale * OsuModDroid.DROID_STACK_OFFSET_MULTIPLIER));
            });

            AddAssert("HR and DA compose identically to osu!droid regardless of mod order", () =>
            {
                var hrA = (HitCircle)droidHardRockA.HitObjects[0];
                var hrB = (HitCircle)droidHardRockB.HitObjects[0];
                var da = (HitCircle)droidDiffAdjust.HitObjects[0];

                return Precision.AlmostEquals(hrA.Scale, OsuModDroid.CalculateDroidScale(5.2f))
                       && Precision.AlmostEquals(hrA.Scale, hrB.Scale)
                       && Precision.AlmostEquals(da.Scale, OsuModDroid.CalculateDroidScale(6.5f));
            });

            AddAssert("exact unfloored DroidHitWindow and PreciseDroidHitWindow applied", () =>
            {
                var drCircle = (HitCircle)droidBeatmap.HitObjects[0];
                var prCircle = (HitCircle)preciseDroidBeatmap.HitObjects[0];

                return drCircle.HitWindows is OsuDroidHitWindows
                       && Precision.AlmostEquals(drCircle.HitWindows.WindowFor(HitResult.Great), 60.0)
                       && Precision.AlmostEquals(drCircle.HitWindows.WindowFor(HitResult.Ok), 120.0)
                       && Precision.AlmostEquals(drCircle.HitWindows.WindowFor(HitResult.Meh), 220.0)
                       && Precision.AlmostEquals(prCircle.HitWindows.WindowFor(HitResult.Great), 37.0)
                       && Precision.AlmostEquals(prCircle.HitWindows.WindowFor(HitResult.Ok), 96.0)
                       && Precision.AlmostEquals(prCircle.HitWindows.WindowFor(HitResult.Meh), 150.0);
            });

            AddAssert("slider and all nested slider objects use droid scale, 2.0x follow area, and classic behaviour", () =>
            {
                float expectedScale = OsuModDroid.CalculateDroidScale(4f);
                var drSlider = (Slider)droidBeatmap.HitObjects[2];

                return drSlider.ClassicSliderBehaviour
                       && drSlider.HeadCircle.ClassicSliderBehaviour
                       && Precision.AlmostEquals(drSlider.FollowCircleArea, OsuModDroid.DROID_FOLLOW_AREA)
                       && Precision.AlmostEquals(drSlider.Scale, expectedScale)
                       && drSlider.NestedHitObjects.OfType<OsuHitObject>().All(n =>
                           Precision.AlmostEquals(n.Scale, expectedScale)
                           && Precision.AlmostEquals(n.StackOffsetMultiplier, OsuModDroid.DROID_STACK_OFFSET_MULTIPLIER));
            });

            AddAssert("spinner uses osu!droid 2 + 0.2 * OD RPS formula", () =>
            {
                // At OD 8, rps = 2 + 0.2 * 8 = 3.6 -> 3.6 * 2.5s = 9 spins.
                var drSpinner = (Spinner)droidBeatmap.HitObjects[3];
                return drSpinner.SpinsRequired == 9;
            });

            AddAssert("display difficulty and attributes reflect osu!droid CS and radius", () =>
            {
                var ruleset = new OsuRuleset();
                var mods = new Mod[] { new OsuModDroid() };
                var adjusted = ruleset.GetAdjustedDisplayDifficulty(standardBeatmap.BeatmapInfo, mods);
                var csAttribute = ruleset.GetBeatmapAttributesForDisplay(standardBeatmap.BeatmapInfo, mods).First(a => a.Acronym == "CS");

                return Precision.AlmostEquals(adjusted.CircleSize, 4f - OsuModDroid.DROID_STANDARD_CS_OFFSET)
                       && csAttribute.AdjustedValue < csAttribute.OriginalValue;
            });
        }

        [Test]
        public void TestDrawableCircleAndSliderVisualScaleInGameplay()
        {
            loadDroidPlayer(new OsuBeatmap
            {
                BeatmapInfo =
                {
                    Difficulty = new BeatmapDifficulty
                    {
                        CircleSize = 4f,
                        OverallDifficulty = 7f,
                        ApproachRate = 8f,
                    },
                },
                HitObjects =
                {
                    new HitCircle { StartTime = 1000, Position = new Vector2(160, 192) },
                    new Slider
                    {
                        StartTime = 1200,
                        Position = new Vector2(300, 192),
                        Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }),
                    },
                },
            });

            seekAndWait(950);

            AddAssert("live drawable hit circle and slider body match droid scale", () =>
            {
                float expectedScale = OsuModDroid.CalculateDroidScale(4f);
                const float argon_base_path_radius = ArgonMainCirclePiece.OUTER_GRADIENT_SIZE / 2;

                var playfield = ((DrawableOsuRuleset)currentPlayer.DrawableRuleset).Playfield;
                var drawableCircle = playfield.HitObjectContainer.AliveObjects.OfType<DrawableHitCircle>().Single(c => c is not DrawableSliderHead);
                var drawableSlider = playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Single();
                float expectedScreenDiameter = playfield.HitObjectContainer.ToScreenSpace(new Vector2(OsuHitObject.OBJECT_RADIUS * 2 * expectedScale, 0)).X
                                               - playfield.HitObjectContainer.ToScreenSpace(Vector2.Zero).X;

                return Precision.AlmostEquals(drawableCircle.ScaleBindable.Value, expectedScale)
                       && Precision.AlmostEquals(drawableCircle.HitArea.ScreenSpaceDrawQuad.Width, expectedScreenDiameter, 0.5f)
                       && Precision.AlmostEquals(drawableSlider.ScaleBindable.Value, expectedScale)
                       && Precision.AlmostEquals(drawableSlider.HeadCircle.ScaleBindable.Value, expectedScale)
                       && drawableSlider.SliderBody != null
                       && Precision.AlmostEquals(drawableSlider.SliderBody.PathRadius, argon_base_path_radius * expectedScale, 0.1f);
            });
        }

        [Test]
        public void TestDifficultyAndPerformanceReduction()
        {
            double noModStars = 0;
            double droidStars = 0;
            double relaxStars = 0;
            double droidRelaxStars = 0;
            double noModPp = 0;
            double droidPp = 0;
            double relaxPp = 0;
            double droidRelaxPp = 0;

            AddStep("calculate star rating and pp", () =>
            {
                var ruleset = new OsuRuleset();
                var rawBeatmap = new OsuBeatmap
                {
                    BeatmapInfo =
                    {
                        Difficulty = new BeatmapDifficulty
                        {
                            CircleSize = 4.5f,
                            OverallDifficulty = 9f,
                            ApproachRate = 9.3f,
                            DrainRate = 6f,
                        },
                        Ruleset = ruleset.RulesetInfo,
                    },
                };

                for (int i = 0; i < 40; i++)
                {
                    rawBeatmap.HitObjects.Add(new HitCircle
                    {
                        StartTime = 1000 + i * 150,
                        Position = new Vector2(100 + (i % 2) * 280, 120 + (i % 3) * 70),
                    });
                }

                var working = CreateWorkingBeatmap(rawBeatmap);
                var diffCalc = ruleset.CreateDifficultyCalculator(working);
                var perfCalc = ruleset.CreatePerformanceCalculator()!;

                var noModDiff = diffCalc.Calculate(Array.Empty<Mod>());
                var droidDiff = diffCalc.Calculate(new Mod[] { new OsuModDroid() });
                var relaxDiff = diffCalc.Calculate(new Mod[] { new OsuModRelax() });
                var droidRelaxDiff = diffCalc.Calculate(new Mod[] { new OsuModDroid(), new OsuModRelax() });

                noModStars = noModDiff.StarRating;
                droidStars = droidDiff.StarRating;
                relaxStars = relaxDiff.StarRating;
                droidRelaxStars = droidRelaxDiff.StarRating;

                var baseScore = new ScoreInfo(rawBeatmap.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Accuracy = 1.0,
                    MaxCombo = 40,
                    Statistics = new Dictionary<HitResult, int>
                    {
                        [HitResult.Great] = 40,
                        [HitResult.Ok] = 0,
                        [HitResult.Meh] = 0,
                        [HitResult.Miss] = 0,
                    },
                };

                var droidScore = baseScore.DeepClone();
                droidScore.Mods = new Mod[] { new OsuModDroid() };

                var relaxScore = baseScore.DeepClone();
                relaxScore.Mods = new Mod[] { new OsuModRelax() };

                var droidRelaxScore = baseScore.DeepClone();
                droidRelaxScore.Mods = new Mod[] { new OsuModDroid(), new OsuModRelax() };

                noModPp = perfCalc.Calculate(baseScore, noModDiff).Total;
                droidPp = perfCalc.Calculate(droidScore, droidDiff).Total;
                relaxPp = perfCalc.Calculate(relaxScore, relaxDiff).Total;
                droidRelaxPp = perfCalc.Calculate(droidRelaxScore, droidRelaxDiff).Total;
            });

            AddAssert("droid star rating is lower", () => droidStars < noModStars && droidStars > 0);
            AddAssert("droid pp is lower", () => droidPp < noModPp && droidPp > 0);
            AddAssert("droid + relax star rating is lower than relax", () => droidRelaxStars < relaxStars && droidRelaxStars > 0);
            AddAssert("droid + relax pp is lower than relax", () => droidRelaxPp < relaxPp && droidRelaxPp > 0);
        }

        [Test]
        public void TestThreeCursorStreamAndFourthCursorCap()
        {
            var streamCenter = OsuPlayfield.BASE_SIZE / 2;

            loadDroidPlayer(new OsuBeatmap
            {
                StackLeniency = 0,
                BeatmapInfo =
                {
                    Difficulty = new BeatmapDifficulty
                    {
                        CircleSize = 4f,
                        OverallDifficulty = 8f,
                        ApproachRate = 8f,
                    },
                },
                HitObjects =
                {
                    new HitCircle { StartTime = 1000, Position = streamCenter },
                    new HitCircle { StartTime = 1080, Position = streamCenter },
                    new HitCircle { StartTime = 1160, Position = streamCenter },
                    new HitCircle { StartTime = 1240, Position = streamCenter },
                },
            });

            Vector2 screenCenter = Vector2.Zero;
            Vector2 emptyCorner = Vector2.Zero;

            AddStep("compute touch positions", () =>
            {
                var quad = currentPlayer.DrawableRuleset.Playfield.ScreenSpaceDrawQuad;
                screenCenter = quad.Centre;
                emptyCorner = quad.TopLeft + new Vector2(15, 15);
            });

            seekAndWait(1000);
            AddStep("cursor 1 hits circle 1 on stack", () => InputManager.BeginTouch(new Touch(TouchSource.Touch1, screenCenter)));

            seekAndWait(1080);
            AddStep("cursor 2 taps empty space", () => InputManager.BeginTouch(new Touch(TouchSource.Touch2, emptyCorner)));

            seekAndWait(1160);
            AddStep("cursor 3 taps empty space while 1 and 2 remain down", () => InputManager.BeginTouch(new Touch(TouchSource.Touch3, emptyCorner + new Vector2(10, 0))));

            seekAndWait(1240);
            AddStep("4th simultaneous cursor is ignored while 3 cursors are down", () => InputManager.BeginTouch(new Touch(TouchSource.Touch4, emptyCorner + new Vector2(20, 0))));
            AddAssert("4th cursor did not hit circle 4", () => currentPlayer.Score.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Great) == 3);

            AddStep("release cursor 2 and tap again for circle 4", () =>
            {
                InputManager.EndTouch(new Touch(TouchSource.Touch2, emptyCorner));
                InputManager.BeginTouch(new Touch(TouchSource.Touch2, emptyCorner));
            });

            seekAndWait(1300);
            AddAssert("all 4 stream circles hit as Great", () => currentPlayer.Score.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Great) == 4);
        }

        [Test]
        public void TestDirectTapThenDragAimFingerHitsNextCircleViaTapFinger()
        {
            var pos1 = new Vector2(140, 192);
            var pos2 = new Vector2(372, 192);

            loadDroidPlayer(new OsuBeatmap
            {
                BeatmapInfo =
                {
                    Difficulty = new BeatmapDifficulty
                    {
                        CircleSize = 4f,
                        OverallDifficulty = 8f,
                        ApproachRate = 8f,
                    },
                },
                HitObjects =
                {
                    new HitCircle { StartTime = 1000, Position = pos1 },
                    new HitCircle { StartTime = 1250, Position = pos2 },
                },
            });

            Vector2 screenPos1 = Vector2.Zero;
            Vector2 screenPos2 = Vector2.Zero;
            Vector2 emptyCorner = Vector2.Zero;

            AddStep("compute screen positions", () =>
            {
                var playfield = ((DrawableOsuRuleset)currentPlayer.DrawableRuleset).Playfield;
                screenPos1 = playfield.ToScreenSpace(pos1);
                screenPos2 = playfield.ToScreenSpace(pos2);
                emptyCorner = playfield.ScreenSpaceDrawQuad.TopLeft + new Vector2(15, 15);
            });

            seekAndWait(1000);
            AddStep("finger 1 directly taps circle 1 and stays held", () => InputManager.BeginTouch(new Touch(TouchSource.Touch1, screenPos1)));

            seekAndWait(1150);
            AddStep("finger 1 drags across playfield onto circle 2", () => InputManager.MoveTouchTo(new Touch(TouchSource.Touch1, screenPos2)));

            seekAndWait(1250);
            AddStep("finger 2 taps empty corner to hit circle 2", () => InputManager.BeginTouch(new Touch(TouchSource.Touch2, emptyCorner)));

            seekAndWait(1300);
            AddAssert("both circles hit as Great", () => currentPlayer.Score.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Great) == 2);
        }

        [Test]
        public void TestSliderHoldTransferAndEmptySpaceTapDoesNotBreakSlider()
        {
            var startPos = new Vector2(180, 192);
            var endOffset = new Vector2(100, 0);

            loadDroidPlayer(new OsuBeatmap
            {
                BeatmapInfo =
                {
                    Difficulty = new BeatmapDifficulty
                    {
                        CircleSize = 4f,
                        OverallDifficulty = 7f,
                        ApproachRate = 8f,
                    },
                },
                HitObjects =
                {
                    new Slider
                    {
                        StartTime = 1000,
                        Position = startPos,
                        Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, endOffset }),
                    },
                },
            });

            Vector2 screenStart = Vector2.Zero;
            Vector2 screenEnd = Vector2.Zero;
            Vector2 emptyCorner = Vector2.Zero;
            double sliderDuration = 0;

            AddStep("compute screen positions", () =>
            {
                var playfield = ((DrawableOsuRuleset)currentPlayer.DrawableRuleset).Playfield;
                screenStart = playfield.ToScreenSpace(startPos);
                screenEnd = playfield.ToScreenSpace(startPos + endOffset);
                emptyCorner = playfield.ScreenSpaceDrawQuad.TopLeft + new Vector2(15, 15);
                sliderDuration = ((Slider)currentPlayer.DrawableRuleset.Objects.First()).Duration;
            });

            // Rest finger 1 before the 400ms miss window so it does not hit the slider head early.
            seekAndWait(500);
            AddStep("aiming finger rests on slider start", () => InputManager.BeginTouch(new Touch(TouchSource.Touch1, screenStart)));

            seekAndWait(1000);
            AddAssert("slider head not judged before tap", () =>
            {
                var slider = ((DrawableOsuRuleset)currentPlayer.DrawableRuleset).Playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Single();
                return !slider.HeadCircle.Judged;
            });

            AddStep("tapping finger taps empty space and lifts immediately", () =>
            {
                var tap = new Touch(TouchSource.Touch2, emptyCorner);
                InputManager.BeginTouch(tap);
                InputManager.EndTouch(tap);
            });

            AddAssert("slider head hit and hold transferred to aiming finger", () =>
            {
                var slider = ((DrawableOsuRuleset)currentPlayer.DrawableRuleset).Playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Single();
                return slider.HeadCircle.IsHit && slider.SliderInputManager.Tracking;
            });

            AddStep("drag aiming finger to slider midpoint and tap empty space with second finger", () =>
            {
                InputManager.MoveTouchTo(new Touch(TouchSource.Touch1, Vector2.Lerp(screenStart, screenEnd, 0.5f)));
                seekClock(1000 + sliderDuration * 0.5);

                var midSliderTap = new Touch(TouchSource.Touch2, emptyCorner);
                InputManager.BeginTouch(midSliderTap);
                InputManager.EndTouch(midSliderTap);
            });

            AddStep("drag aiming finger to slider end", () =>
            {
                InputManager.MoveTouchTo(new Touch(TouchSource.Touch1, screenEnd));
                seekClock(1000 + sliderDuration + 50);
            });

            AddUntilStep("slider judged as Great", () => currentPlayer.Score.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Great) == 1);
        }

        [Test]
        public void TestDroidWithRelaxMod()
        {
            var pos1 = new Vector2(140, 192);
            var pos2 = new Vector2(372, 192);
            var sliderStart = new Vector2(160, 192);
            var sliderEndOffset = new Vector2(100, 0);

            AddAssert("DR and RX are compatible while DR and AP remain incompatible", () =>
                ModUtils.CheckCompatibleSet(new Mod[] { new OsuModDroid(), new OsuModRelax() })
                && !ModUtils.CheckCompatibleSet(new Mod[] { new OsuModDroid(), new OsuModAutopilot() }));

            loadDroidPlayer(new OsuBeatmap
            {
                StackLeniency = 0,
                BeatmapInfo =
                {
                    Difficulty = new BeatmapDifficulty
                    {
                        CircleSize = 4f,
                        OverallDifficulty = 8f,
                        ApproachRate = 8f,
                    },
                },
                HitObjects =
                {
                    new HitCircle { StartTime = 1000, Position = pos1 },
                    new HitCircle { StartTime = 1250, Position = pos2 },
                    new HitCircle { StartTime = 1600, Position = pos2 },
                    new Slider
                    {
                        StartTime = 2200,
                        Position = sliderStart,
                        Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, sliderEndOffset }),
                    },
                },
            }, new Mod[] { new OsuModDroid(), new OsuModRelax() });

            Vector2 screenPos1 = Vector2.Zero;
            Vector2 screenPos2 = Vector2.Zero;
            Vector2 screenSliderStart = Vector2.Zero;
            Vector2 screenSliderEnd = Vector2.Zero;
            double sliderDuration = 0;

            AddStep("compute screen positions", () =>
            {
                var playfield = ((DrawableOsuRuleset)currentPlayer.DrawableRuleset).Playfield;
                screenPos1 = playfield.ToScreenSpace(pos1);
                screenPos2 = playfield.ToScreenSpace(pos2);
                screenSliderStart = playfield.ToScreenSpace(sliderStart);
                screenSliderEnd = playfield.ToScreenSpace(sliderStart + sliderEndOffset);
                sliderDuration = ((Slider)currentPlayer.DrawableRuleset.Objects.OfType<Slider>().Single()).Duration;
            });

            seekAndWait(600);
            AddStep("hold finger 1 on circle 1 and finger 2 on circle 2", () =>
            {
                InputManager.BeginTouch(new Touch(TouchSource.Touch1, screenPos1));
                InputManager.BeginTouch(new Touch(TouchSource.Touch2, screenPos2));
            });

            seekAndWait(1000);
            AddUntilStep("finger 1 auto-hit circle 1 as Great", () => currentPlayer.Score.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Great) == 1);

            seekAndWait(1250);
            AddUntilStep("finger 2 auto-hit circle 2 as Great", () => currentPlayer.Score.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Great) == 2);

            AddStep("lift all fingers off screen", () =>
            {
                InputManager.EndTouch(new Touch(TouchSource.Touch1, screenPos1));
                InputManager.EndTouch(new Touch(TouchSource.Touch2, screenPos2));
            });

            seekAndWait(1900);
            AddAssert("circle 3 missed when no finger is touching screen", () =>
                currentPlayer.Score.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Great) == 2
                && currentPlayer.Score.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Miss) == 1);

            seekAndWait(2100);
            AddStep("place finger 1 on slider start", () => InputManager.BeginTouch(new Touch(TouchSource.Touch1, screenSliderStart)));

            seekAndWait(2200);
            AddUntilStep("slider head auto-hit and tracking", () =>
            {
                var slider = ((DrawableOsuRuleset)currentPlayer.DrawableRuleset).Playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Single();
                return slider.HeadCircle.IsHit && slider.SliderInputManager.Tracking;
            });

            AddStep("drag finger 1 to slider midpoint", () =>
            {
                InputManager.MoveTouchTo(new Touch(TouchSource.Touch1, Vector2.Lerp(screenSliderStart, screenSliderEnd, 0.5f)));
                seekClock(2200 + sliderDuration * 0.5);
            });

            AddStep("drag finger 1 to slider end", () =>
            {
                InputManager.MoveTouchTo(new Touch(TouchSource.Touch1, screenSliderEnd));
                seekClock(2200 + sliderDuration + 50);
            });

            AddUntilStep("slider judged as Great", () => currentPlayer.Score.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Great) == 3);
        }

        [Test]
        public void TestNonTouchMouseClickBlockedInLivePlay()
        {
            loadDroidPlayer(new OsuBeatmap
            {
                HitObjects =
                {
                    new HitCircle
                    {
                        StartTime = 1000,
                        Position = OsuPlayfield.BASE_SIZE / 2,
                    },
                },
            });

            seekAndWait(1000);
            AddStep("attempt mouse move and click on circle", () =>
            {
                InputManager.MoveMouseTo(currentPlayer.DrawableRuleset.Playfield.ScreenSpaceDrawQuad.Centre);
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("circle was not hit by mouse", () => currentPlayer.Score.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Great) == 0);
        }

        private void seekClock(double time)
        {
            manualClock.CurrentTime = time;
            currentPlayer.GameplayClockContainer.Seek(time);
        }

        private void seekAndWait(double time)
        {
            AddStep($"seek to {time}", () => seekClock(time));
            AddUntilStep($"wait until {time}", () => currentPlayer.GameplayClockContainer.CurrentTime, () => Is.GreaterThanOrEqualTo(time));
        }

        private void loadDroidPlayer(OsuBeatmap osuBeatmap, Mod[]? mods = null)
        {
            AddStep("load player with osu!droid mod", () =>
            {
                manualClock.CurrentTime = 0;
                SelectedMods.Value = mods ?? new Mod[] { new OsuModDroid() };
                Beatmap.Value = CreateWorkingBeatmap(osuBeatmap);
                LoadScreen(currentPlayer = new ScoreAccessibleSoloPlayer());
            });

            AddUntilStep("wait until player is loaded", () => currentPlayer.IsCurrentScreen());
            AddUntilStep("wait until gameplay clock running", () => currentPlayer.GameplayClockContainer.IsRunning);
        }

        private partial class ScoreAccessibleSoloPlayer : SoloPlayer
        {
            public new GameplayClockContainer GameplayClockContainer => base.GameplayClockContainer;

            public new DrawableRuleset DrawableRuleset => base.DrawableRuleset;

            protected override bool PauseOnFocusLost => false;

            public ScoreAccessibleSoloPlayer()
                : base(new PlayerConfiguration
                {
                    AllowPause = false,
                    ShowResults = false,
                })
            {
            }
        }
    }
}
