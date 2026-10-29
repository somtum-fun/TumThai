// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.StateChanges;
using osu.Framework.Testing;
using osu.Game.Replays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Gameplay;
using osu.Game.Tests.Mods;
using osuTK;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneCustomRulesetTouchReplay : OsuManualInputManagerTestScene
    {
        private DirectTouchRulesetInputManager recordingManager;
        private DirectTouchRulesetInputManager playbackManager;
        private TouchProbeConsumer recordingProbe;
        private TouchProbeConsumer playbackProbe;
        private DirectTouchReplayRecorder recorder;
        private Replay replay;

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("dispose recorder", () =>
            {
                recorder?.RemoveAndDisposeImmediately();
                recorder = null;
            });
        }

        [Test]
        public void TestLiveMultiTouchRecordAndReplay()
        {
            AddStep("setup recording and playback", () =>
            {
                recorder?.RemoveAndDisposeImmediately();
                replay = new Replay();

                var gameplayState = TestGameplayState.Create(new OsuRuleset());
                gameplayState.Score.Replay = replay;

                var customRulesetInfo = TestCustomisableModRuleset.CreateTestRulesetInfo();

                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(GameplayState), gameplayState) },
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                recordingManager = new DirectTouchRulesetInputManager(customRulesetInfo)
                                {
                                    Recorder = recorder = new DirectTouchReplayRecorder(gameplayState.Score)
                                    {
                                        ScreenSpaceToGamefield = pos => recordingManager.ToLocalSpace(pos),
                                    },
                                    Child = recordingProbe = new TouchProbeConsumer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                    },
                                },
                            },
                            new Drawable[]
                            {
                                playbackManager = new DirectTouchRulesetInputManager(customRulesetInfo)
                                {
                                    Child = playbackProbe = new TouchProbeConsumer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                    },
                                },
                            },
                        },
                    },
                };
            });

            AddAssert("direct touch replay enabled", () => recordingManager.UsesDirectTouchReplay, () => Is.True);

            Vector2 localPointA = new Vector2(60f, 45f);
            Vector2 localPointB = new Vector2(180f, 90f);

            AddStep("begin two-finger touch", () =>
            {
                InputManager.BeginTouch(new Touch(TouchSource.Touch1, recordingManager.ToScreenSpace(localPointA)));
                InputManager.BeginTouch(new Touch(TouchSource.Touch2, recordingManager.ToScreenSpace(localPointB)));
            });

            AddStep("drag first finger", () =>
            {
                InputManager.MoveTouchTo(new Touch(TouchSource.Touch1, recordingManager.ToScreenSpace(localPointA + new Vector2(30f, 15f))));
            });

            AddStep("release both fingers", () =>
            {
                InputManager.EndTouch(new Touch(TouchSource.Touch1, recordingManager.ToScreenSpace(localPointA + new Vector2(30f, 15f))));
                InputManager.EndTouch(new Touch(TouchSource.Touch2, recordingManager.ToScreenSpace(localPointB)));
            });

            AddAssert("recording probe saw 2 touch downs", () => recordingProbe.TouchDownCount, () => Is.EqualTo(2));
            AddAssert("custom touch frames recorded", () => replay.CustomTouchFrames?.Count ?? 0, () => Is.GreaterThanOrEqualTo(3));
            AddAssert("frames preserved despite unchanging mouse/actions", () => replay.Frames.Count, () => Is.GreaterThanOrEqualTo(3));

            AddStep("play back recorded replay", () =>
            {
                replay.HasReceivedAllFrames = true;
                var handler = new DirectTouchReplayInputHandler(replay.DeepClone())
                {
                    GamefieldToScreenSpace = pos => playbackManager.ToScreenSpace(pos),
                };
                playbackManager.ReplayInputHandler = handler;

                foreach (var frame in replay.Frames.OrderBy(f => f.Time))
                {
                    while (handler.SetFrameFromTime(frame.Time) != frame.Time)
                    {
                    }

                    // Manually flush playback input manager per frame step.
                    Schedule(() => { });
                }
            });

            AddStep("replay chord frame", () =>
            {
                double chordTime = replay.CustomTouchFrames!.First(f => f.Touches.Length == 2).Time;
                var handler = new DirectTouchReplayInputHandler(replay.DeepClone())
                {
                    GamefieldToScreenSpace = pos => playbackManager.ToScreenSpace(pos),
                };
                playbackManager.ReplayInputHandler = handler;

                while (handler.SetFrameFromTime(chordTime) != chordTime)
                {
                }
            });

            AddUntilStep("playback probe received both touches at exact local positions", () =>
                playbackProbe.TouchDownCount >= 2
                && playbackProbe.LastLocalPositions.TryGetValue(TouchSource.Touch1, out var p1)
                && Vector2.Distance(p1, localPointA) < 1f
                && playbackProbe.LastLocalPositions.TryGetValue(TouchSource.Touch2, out var p2)
                && Vector2.Distance(p2, localPointB) < 1f);
        }

        private enum DirectTouchAction
        {
            Button1,
        }

        private partial class DirectTouchRulesetInputManager : RulesetInputManager<DirectTouchAction>
        {
            protected override bool MapMouseToLatestTouch => false;

            public DirectTouchRulesetInputManager(RulesetInfo ruleset)
                : base(ruleset, 0, SimultaneousBindingMode.Unique)
            {
            }
        }

        private partial class TouchProbeConsumer : CompositeDrawable
        {
            public int TouchDownCount { get; private set; }
            public readonly Dictionary<TouchSource, Vector2> LastLocalPositions = new Dictionary<TouchSource, Vector2>();

            public override bool HandlePositionalInput => true;

            protected override bool OnTouchDown(TouchDownEvent e)
            {
                TouchDownCount++;
                LastLocalPositions[e.Touch.Source] = e.Touch.Position;
                return true;
            }

            protected override void OnTouchMove(TouchMoveEvent e)
            {
                LastLocalPositions[e.Touch.Source] = e.Touch.Position;
                base.OnTouchMove(e);
            }
        }

        private class DirectTouchFrame : ReplayFrame
        {
            public Vector2 Position;

            public DirectTouchFrame(double time, Vector2 position)
                : base(time)
            {
                Position = position;
            }

            public override bool IsEquivalentTo(ReplayFrame other)
                => other is DirectTouchFrame df && Position == df.Position;
        }

        private partial class DirectTouchReplayRecorder : ReplayRecorder<DirectTouchAction>
        {
            public DirectTouchReplayRecorder(Score target)
                : base(target)
            {
            }

            protected override ReplayFrame HandleFrame(Vector2 mousePosition, List<DirectTouchAction> actions, ReplayFrame previousFrame)
                => new DirectTouchFrame(Time.Current, mousePosition);
        }

        private class DirectTouchReplayInputHandler : FramedReplayInputHandler<DirectTouchFrame>
        {
            public DirectTouchReplayInputHandler(Replay replay)
                : base(replay)
            {
            }

            protected override void CollectReplayInputs(List<IInput> inputs)
            {
                inputs.Add(new MousePositionAbsoluteInput
                {
                    Position = GamefieldToScreenSpace(CurrentFrame?.Position ?? Vector2.Zero),
                });
            }
        }
    }
}
