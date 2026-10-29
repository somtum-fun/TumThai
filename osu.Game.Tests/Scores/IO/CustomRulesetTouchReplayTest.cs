// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.StateChanges;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Input.Handlers;
using osu.Game.Online.API;
using osu.Game.Replays;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Resources;
using osuTK;

namespace osu.Game.Tests.Scores.IO
{
    [TestFixture]
    public partial class CustomRulesetTouchReplayTest
    {
        [Test]
        public void TestUsesDirectTouchReplayDetection()
        {
            var officialRuleset = new OsuRuleset().RulesetInfo;
            var customRuleset = new TestDirectTouchRuleset().RulesetInfo;

            var officialUnmapped = new TestConfigurableInputManager(officialRuleset, mapMouseToLatestTouch: false, useCustomTouchManager: false);
            var customMouseMapped = new TestConfigurableInputManager(customRuleset, mapMouseToLatestTouch: true, useCustomTouchManager: false);
            var customEventManager = new TestConfigurableInputManager(customRuleset, mapMouseToLatestTouch: false, useCustomTouchManager: true);
            var customDirectTouch = new TestConfigurableInputManager(customRuleset, mapMouseToLatestTouch: false, useCustomTouchManager: false);

            Assert.Multiple(() =>
            {
                Assert.That(officialUnmapped.UsesDirectTouchReplay, Is.False);
                Assert.That(customMouseMapped.UsesDirectTouchReplay, Is.False);
                Assert.That(customEventManager.UsesDirectTouchReplay, Is.False);
                Assert.That(customDirectTouch.UsesDirectTouchReplay, Is.True);
            });
        }

        [TestCase(LegacyBeatmapDecoder.LATEST_VERSION)]
        [TestCase(4)]
        public void TestEncodeDecodeAndPlaybackMultiTouchRoundTrip(int beatmapVersion)
        {
            var ruleset = new TestDirectTouchRuleset();
            var scoreInfo = TestResources.CreateTestScoreInfo(ruleset.RulesetInfo);
            scoreInfo.Mods = Array.Empty<Mod>();
            scoreInfo.APIMods = Array.Empty<APIMod>();
            scoreInfo.Statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 10,
            };

            var beatmap = new TestBeatmap(new OsuRuleset().RulesetInfo)
            {
                BeatmapVersion = beatmapVersion,
            };

            var score = new Score
            {
                ScoreInfo = scoreInfo,
                Replay = new Replay
                {
                    HasReceivedAllFrames = true,
                    Frames = new List<ReplayFrame>
                    {
                        new TestDirectTouchReplayFrame(0, Vector2.Zero),
                        new TestDirectTouchReplayFrame(1000, Vector2.Zero),
                        new TestDirectTouchReplayFrame(1050, Vector2.Zero),
                        new TestDirectTouchReplayFrame(1100, Vector2.Zero),
                        new TestDirectTouchReplayFrame(1500.2, Vector2.Zero),
                        new TestDirectTouchReplayFrame(1500.4, Vector2.Zero),
                        new TestDirectTouchReplayFrame(2000, Vector2.Zero),
                    },
                    CustomTouchFrames = new List<ReplayTouchFrame>
                    {
                        new ReplayTouchFrame(1000, new[]
                        {
                            new ReplayTouchPoint(TouchSource.Touch1, new Vector2(300f, 0f)),
                            new ReplayTouchPoint(TouchSource.Touch2, new Vector2(-300f, 0f)),
                        }),
                        new ReplayTouchFrame(1050, new[]
                        {
                            new ReplayTouchPoint(TouchSource.Touch1, new Vector2(150f, 150f)),
                        }),
                        new ReplayTouchFrame(1100, Array.Empty<ReplayTouchPoint>()),
                        // Sub-millisecond flick tap should preserve a >= 1 ms active window after integer rounding.
                        new ReplayTouchFrame(1500.2, new[]
                        {
                            new ReplayTouchPoint(TouchSource.Touch3, new Vector2(12.5f, -25.25f)),
                        }),
                        new ReplayTouchFrame(1500.4, Array.Empty<ReplayTouchPoint>()),
                    },
                },
            };

            Score decoded;

            using (var ms = new MemoryStream())
            {
                new LegacyScoreEncoder(score, beatmap).Encode(ms, leaveOpen: true);
                ms.Position = 0;
                decoded = new TestCustomScoreDecoder(ruleset, beatmapVersion).Parse(ms);
            }

            Assert.That(decoded.Replay.CustomTouchFrames, Is.Not.Null);
            Assert.That(decoded.Replay.CustomTouchFrames!.Count, Is.EqualTo(5));
            Assert.That(decoded.Replay.CustomTouchFrames[3].Time, Is.EqualTo(1500));
            Assert.That(decoded.Replay.CustomTouchFrames[4].Time, Is.EqualTo(1501));
            Assert.That(decoded.Replay.Frames.Any(f => f.Time == 1501), Is.True, "Bumped sub-ms flick release must also exist in Replay.Frames for frame-accurate stepping");

            var handler = new TestDirectTouchReplayInputHandler(decoded.Replay)
            {
                FrameAccuratePlayback = true,
                GamefieldToScreenSpace = p => p * 2f + new Vector2(100f, 50f),
            };

            advanceTo(handler, 500);
            var inputsAt500 = collectTouchInputs(handler);
            Assert.That(inputsAt500.Any(i => i.Activate), Is.False);

            // Chord down at t = 1000.
            advanceTo(handler, 1000);
            var inputsAt1000 = collectTouchInputs(handler);
            var active1000 = inputsAt1000.Single(i => i.Activate).Touches.ToDictionary(t => t.Source, t => t.Position);
            Assert.That(active1000.Keys, Is.EquivalentTo(new[] { TouchSource.Touch1, TouchSource.Touch2 }));
            Assert.That(active1000[TouchSource.Touch1], Is.EqualTo(new Vector2(700f, 50f)));
            Assert.That(active1000[TouchSource.Touch2], Is.EqualTo(new Vector2(-500f, 50f)));

            // Mid-slide interpolation at t = 1020 (outside the 20 ms important window before 1050):
            // Touch1 interpolates 40% from (300, 0) to (150, 150) => (240, 60) => screen (580, 170).
            advanceTo(handler, 1020);
            var active1020 = collectTouchInputs(handler).Single(i => i.Activate).Touches.ToDictionary(t => t.Source, t => t.Position);
            Assert.That(active1020[TouchSource.Touch1].X, Is.EqualTo(580f).Within(0.01f));
            Assert.That(active1020[TouchSource.Touch1].Y, Is.EqualTo(170f).Within(0.01f));

            // Touch2 releases at t = 1050 while Touch1 stays active.
            advanceTo(handler, 1050);
            var inputsAt1050 = collectTouchInputs(handler);
            Assert.That(inputsAt1050.Single(i => !i.Activate).Touches.Select(t => t.Source), Is.EquivalentTo(new[] { TouchSource.Touch2 }));
            Assert.That(inputsAt1050.Single(i => i.Activate).Touches.Select(t => t.Source), Is.EquivalentTo(new[] { TouchSource.Touch1 }));

            // All touches release at t = 1100.
            advanceTo(handler, 1100);
            var inputsAt1100 = collectTouchInputs(handler);
            Assert.That(inputsAt1100.Single(i => !i.Activate).Touches.Select(t => t.Source), Is.EquivalentTo(new[] { TouchSource.Touch1 }));
            Assert.That(inputsAt1100.Any(i => i.Activate), Is.False);

            // Fast-forwarding across 1500..1501 must step precisely through 1500 (down) and 1501 (up).
            double? step1 = handler.SetFrameFromTime(1520);
            Assert.That(step1, Is.EqualTo(1500));
            var flickDown = collectTouchInputs(handler);
            Assert.That(flickDown.Single(i => i.Activate).Touches.Single().Source, Is.EqualTo(TouchSource.Touch3));
            Assert.That(flickDown.Single(i => i.Activate).Touches.Single().Position, Is.EqualTo(new Vector2(125f, -0.5f)));

            double? step2 = handler.SetFrameFromTime(1520);
            Assert.That(step2, Is.EqualTo(1501));
            var flickUp = collectTouchInputs(handler);
            Assert.That(flickUp.Single(i => !i.Activate).Touches.Single().Source, Is.EqualTo(TouchSource.Touch3));
            Assert.That(flickUp.Any(i => i.Activate), Is.False);

            // Seeking backwards from an active chord back before t = 1000 must deactivate active touch sources.
            rewindTo(handler, 1000);
            Assert.That(collectTouchInputs(handler).Any(i => i.Activate), Is.True);
            rewindTo(handler, 500);
            var afterRewind = collectTouchInputs(handler);
            Assert.That(afterRewind.Any(i => i.Activate), Is.False);
            Assert.That(afterRewind.Single(i => !i.Activate).Touches.Select(t => t.Source), Is.SupersetOf(new[] { TouchSource.Touch1, TouchSource.Touch2 }));
        }

        private static void advanceTo(ReplayInputHandler handler, double targetTime)
        {
            int guard = 0;

            while (guard++ < 32)
            {
                double? res = handler.SetFrameFromTime(targetTime);
                if (res == null || res.Value == targetTime)
                    break;
            }
        }

        private static void rewindTo(ReplayInputHandler handler, double targetTime)
        {
            int guard = 0;

            while (guard++ < 32)
            {
                double? res = handler.SetFrameFromTime(targetTime);
                if (res == null || res.Value == targetTime)
                    break;
            }
        }

        private static List<TouchInput> collectTouchInputs(ReplayInputHandler handler)
        {
            var list = new List<IInput>();
            handler.CollectPendingInputs(list);
            return list.OfType<TouchInput>().ToList();
        }

        private enum DummyAction
        {
            Action1,
        }

        private partial class TestConfigurableInputManager : RulesetInputManager<DummyAction>
        {
            private readonly bool mapMouse;
            private readonly bool customTouchMgr;

            protected override bool MapMouseToLatestTouch => mapMouse;

            public TestConfigurableInputManager(RulesetInfo ruleset, bool mapMouseToLatestTouch, bool useCustomTouchManager)
                : base(ruleset, 0, SimultaneousBindingMode.Unique)
            {
                mapMouse = mapMouseToLatestTouch;
                customTouchMgr = useCustomTouchManager;
            }

            protected override TouchEventManager CreateButtonEventManagerFor(TouchSource source)
                => customTouchMgr ? new CustomTouchEventManager(source) : base.CreateButtonEventManagerFor(source);

            private class CustomTouchEventManager : TouchEventManager
            {
                public CustomTouchEventManager(TouchSource source)
                    : base(source)
                {
                }
            }
        }

        private class TestDirectTouchRuleset : Ruleset
        {
            public override string Description => "Test Direct Touch Ruleset";
            public override string ShortName => "sentakki_test";

            public override IEnumerable<Mod> GetModsFor(ModType type) => Array.Empty<Mod>();

            public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
                => throw new NotImplementedException();

            public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
                => new OsuBeatmapConverter(beatmap, new OsuRuleset());

            public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
                => throw new NotImplementedException();

            public override IConvertibleReplayFrame CreateConvertibleReplayFrame()
                => new TestDirectTouchReplayFrame();
        }

        private class TestDirectTouchReplayFrame : ReplayFrame, IConvertibleReplayFrame
        {
            public Vector2 Position;

            public TestDirectTouchReplayFrame()
            {
            }

            public TestDirectTouchReplayFrame(double time, Vector2 position)
                : base(time)
            {
                Position = position;
            }

            public void FromLegacy(LegacyReplayFrame currentFrame, IBeatmap beatmap, ReplayFrame? lastFrame = null)
            {
                Position = currentFrame.Position;
            }

            public LegacyReplayFrame ToLegacy(IBeatmap beatmap)
                => new LegacyReplayFrame(Time, Position.X, Position.Y, ReplayButtonState.None);

            public override bool IsEquivalentTo(ReplayFrame other)
                => other is TestDirectTouchReplayFrame tf && Position == tf.Position;
        }

        private class TestDirectTouchReplayInputHandler : FramedReplayInputHandler<TestDirectTouchReplayFrame>
        {
            public TestDirectTouchReplayInputHandler(Replay replay)
                : base(replay)
            {
            }

            protected override bool IsImportant(TestDirectTouchReplayFrame frame) => true;

            protected override void CollectReplayInputs(List<IInput> inputs)
            {
                inputs.Add(new MousePositionAbsoluteInput
                {
                    Position = GamefieldToScreenSpace(CurrentFrame?.Position ?? Vector2.Zero),
                });
            }
        }

        private class TestCustomScoreDecoder : LegacyScoreDecoder
        {
            private readonly Ruleset customRuleset;
            private readonly int beatmapVersion;

            public TestCustomScoreDecoder(Ruleset customRuleset, int beatmapVersion)
            {
                this.customRuleset = customRuleset;
                this.beatmapVersion = beatmapVersion;
            }

            protected override Ruleset GetRuleset(int rulesetId) => new OsuRuleset();

            protected override Ruleset? GetRuleset(string shortName)
                => shortName == customRuleset.ShortName ? customRuleset : null;

            protected override WorkingBeatmap GetBeatmap(string md5Hash) => new TestWorkingBeatmap(new Beatmap
            {
                BeatmapInfo = new BeatmapInfo
                {
                    MD5Hash = md5Hash,
                    Ruleset = new OsuRuleset().RulesetInfo,
                    Difficulty = new BeatmapDifficulty(),
                },
                HitObjects = { new HitCircle() },
                BeatmapVersion = beatmapVersion,
            });
        }
    }
}
