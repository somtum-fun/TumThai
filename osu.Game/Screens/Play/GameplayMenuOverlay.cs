// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    public abstract partial class GameplayMenuOverlay : OverlayContainer, IKeyBindingHandler<GlobalAction>
    {
        protected const int TRANSITION_DURATION = 200;

        private const int button_height = 80;
        private const float background_alpha = 0.75f;

        protected override bool BlockScrollInput => false;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        public Action? OnResume { get; init; }
        public Action? OnRetry { get; init; }
        public Action? OnQuit { get; init; }

        /// <summary>
        /// Action that is invoked when <see cref="GlobalAction.Back"/> is triggered.
        /// </summary>
        protected virtual Action BackAction => () =>
        {
            // We prefer triggering the button click as it will animate...
            // but sometimes buttons aren't present (see FailOverlay's constructor as an example).
            if (Buttons.Any())
                Buttons.Last().TriggerClick();
            else
                OnQuit?.Invoke();
        };

        /// <summary>
        /// Action that is invoked when <see cref="GlobalAction.Select"/> is triggered.
        /// </summary>
        protected virtual Action SelectAction => () => InternalButtons.Selected?.TriggerClick();

        public abstract LocalisableString Header { get; }

        public Container FooterContent { get; private set; } = null!;

        protected SelectionCycleFillFlowContainer<DialogButton> InternalButtons = null!;
        public IReadOnlyList<DialogButton> Buttons => InternalButtons;

        private TextFlowContainer playInfoText = null!;
        private Bindable<InterfaceTheme> interfaceTheme = null!;

        [Resolved]
        private GlobalActionContainer globalAction { get; set; } = null!;

        protected GameplayMenuOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, GameHost? host, OsuConfigManager config)
        {
            interfaceTheme = config.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);
            Children = new Drawable[]
            {
                pauseLoop = new SkinnableSound(new SampleInfo("Gameplay/pause-loop"))
                {
                    Looping = true,
                    Volume = { Value = 0 }
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = background_alpha,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = Header,
                                Font = OsuFont.GetFont(typeface: Typeface.TorusAlternate, size: 48, weight: FontWeight.SemiBold),
                                Spacing = new Vector2(5),
                                Origin = Anchor.Centre,
                                Anchor = Anchor.Centre,
                                Colour = colours.Yellow,
                            },
                        },
                        new Drawable[]
                        {
                            InternalButtons = new SelectionCycleFillFlowContainer<DialogButton>
                            {
                                Origin = Anchor.Centre,
                                Anchor = Anchor.Centre,
                                RelativeSizeAxes = Axes.X,
                                Padding = new MarginPadding { Horizontal = 50 },
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(2),
                                Masking = false,
                            },
                        },
                        new Drawable[]
                        {
                            playInfoText = new OsuTextFlowContainer(cp => cp.Font = OsuFont.GetFont(size: 18))
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                TextAnchor = Anchor.Centre,
                                AutoSizeAxes = Axes.Both,
                            }
                        },
                        new Drawable[]
                        {
                            FooterContent = new Container
                            {
                                AutoSizeAxes = Axes.Y,
                                RelativeSizeAxes = Axes.X,
                            },
                        }
                    }
                }
            };

            if (OnResume != null)
                AddButton(GameplayMenuOverlayStrings.Continue, colours.Green, () => OnResume.Invoke(), @"pause-continue", @"pause-continue-click");

            if (OnRetry != null)
                AddButton(GameplayMenuOverlayStrings.Retry, colours.YellowDark, () => OnRetry.Invoke(), @"pause-retry", @"pause-retry-click");

            if (OnQuit != null)
                AddButton(GameplayMenuOverlayStrings.Quit, new Color4(170, 27, 39, 255), () => OnQuit.Invoke(), @"pause-back", @"pause-back-click");

            State.ValueChanged += _ => InternalButtons.Deselect();

            updateInfoText();

            if (host != null)
                windowActive.BindTo(host.IsActive);
        }

        private int retries;

        public int Retries
        {
            set
            {
                if (value == retries)
                    return;

                retries = value;

                if (IsLoaded)
                    updateInfoText();
            }
        }

        protected override void PopIn()
        {
            this.FadeIn(TRANSITION_DURATION, Easing.In);
            updateInfoText();

            startPauseLoop();
        }

        protected override void PopOut()
        {
            this.FadeOut(TRANSITION_DURATION, Easing.In);
            stopPauseLoop();
        }

        protected void AddButton(LocalisableString text, Color4 colour, Action? action, string? stableSpriteName = null, string? stableClickSampleName = null)
        {
            var button = new Button(stableSpriteName, stableClickSampleName)
            {
                Text = text,
                ButtonColour = colour,
                Origin = Anchor.TopCentre,
                Anchor = Anchor.TopCentre,
                Height = button_height,
                Action = delegate
                {
                    action?.Invoke();
                    Hide();
                }
            };

            InternalButtons.Add(button);
        }

        public virtual bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            switch (e.Action)
            {
                case GlobalAction.SelectPrevious:
                    InternalButtons.SelectPrevious();
                    return true;

                case GlobalAction.SelectNext:
                    InternalButtons.SelectNext();
                    return true;

                case GlobalAction.Back:
                    BackAction.Invoke();
                    return true;

                case GlobalAction.Select:
                    SelectAction.Invoke();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        [Resolved]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        private void updateInfoText()
        {
            playInfoText.Clear();
            playInfoText.AddText(GameplayMenuOverlayStrings.RetryCount);
            playInfoText.AddText(retries.ToString(), cp => cp.Font = cp.Font.With(weight: FontWeight.Bold));

            if (getSongProgress() is int progress)
            {
                playInfoText.NewLine();
                playInfoText.AddText(GameplayMenuOverlayStrings.SongProgress);
                playInfoText.AddText($"{progress}%", cp => cp.Font = cp.Font.With(weight: FontWeight.Bold));
            }

            if (gameplayState != null)
            {
                playInfoText.NewLine();
                playInfoText.AddText(BeatmapsetsStrings.ShowScoreboardHeadersAccuracy);
                playInfoText.AddText(": ");
                playInfoText.AddText(gameplayState!.ScoreProcessor.Accuracy.Value.FormatAccuracy(), cp => cp.Font = cp.Font.With(weight: FontWeight.Bold));
            }
        }

        private int? getSongProgress()
        {
            if (gameplayClock == null || gameplayState == null)
                return null;

            (double firstHitTime, double lastHitTime) = gameplayState.Beatmap.CalculatePlayableBounds();

            double playableLength = (lastHitTime - firstHitTime);

            if (playableLength == 0)
                return 0;

            return (int)Math.Clamp(((gameplayClock.CurrentTime - firstHitTime) / playableLength) * 100, 0, 100);
        }

        private partial class Button : DialogButton
        {
            private readonly string? stableSpriteName;
            private readonly string? stableClickSampleName;
            private Sprite? stableSprite;
            private Sprite? leftArrow;
            private Sprite? rightArrow;
            private ISample? hoverSample;
            private ISample? clickSample;
            private IBindable<InterfaceTheme>? themeBindable;

            public Button(string? stableSpriteName = null, string? stableClickSampleName = null)
            {
                this.stableSpriteName = stableSpriteName;
                this.stableClickSampleName = stableClickSampleName;
            }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config, SkinManager skinManager, AudioManager audio)
            {
                themeBindable = config.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);
                // osu!stable's pause menu points at the selected button with arrow-pause
                var arrowTex = skinManager.GetClassicTexture(@"arrow-pause") ?? skinManager.GetClassicTexture(@"play-warningarrow");
                var buttonTex = stableSpriteName != null ? skinManager.GetClassicTexture(stableSpriteName) : null;

                hoverSample = skinManager.GetClassicSample(@"pause-hover", @"menuclick") ?? audio.Samples.Get(@"UI/generic-hover");
                clickSample = stableClickSampleName != null
                    ? (skinManager.GetClassicSample(stableClickSampleName, @"menuhit") ?? audio.Samples.Get(@"UI/generic-select"))
                    : null;

                if (buttonTex != null)
                {
                    AddInternal(stableSprite = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Texture = buttonTex,
                        Alpha = 0,
                    });
                }

                if (arrowTex != null)
                {
                    AddInternal(leftArrow = new Sprite
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreRight,
                        X = -16,
                        // osu!stable draws arrow-pause at its own size
                        Texture = arrowTex,
                        Colour = Color4Extensions.FromHex(@"0072BC"),
                        Alpha = 0,
                    });
                    AddInternal(rightArrow = new Sprite
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Scale = new Vector2(-1, 1),
                        X = 16,
                        // osu!stable draws arrow-pause at its own size
                        Texture = arrowTex,
                        Colour = Color4Extensions.FromHex(@"0072BC"),
                        Alpha = 0,
                    });
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                StateChanged += _ => updateArrowVisibility();

                themeBindable?.BindValueChanged(t =>
                {
                    bool isStable = t.NewValue.IsThemed() && stableSprite != null;
                    if (isStable)
                    {
                        RelativeSizeAxes = Axes.None;
                        Width = 411;
                        Height = 122;
                        Margin = new MarginPadding { Vertical = 4 };
                        stableSprite!.Alpha = 1;
                        foreach (var child in InternalChildren)
                        {
                            if (child != stableSprite && child != leftArrow && child != rightArrow)
                                child.Alpha = 0;
                        }
                    }
                    else
                    {
                        RelativeSizeAxes = Axes.X;
                        Width = 1f;
                        Height = button_height;
                        Margin = default;
                        if (stableSprite != null)
                            stableSprite.Alpha = 0;
                        foreach (var child in InternalChildren)
                        {
                            if (child != stableSprite && child != leftArrow && child != rightArrow)
                                child.Alpha = 1;
                        }
                    }
                    updateArrowVisibility();
                }, true);
            }

            private void updateArrowVisibility()
            {
                bool isStable = themeBindable?.Value.IsThemed() ?? false;
                bool selected = State == SelectionState.Selected;
                bool showArrows = isStable && selected;
                leftArrow?.FadeTo(showArrows ? 1 : 0, 120, Easing.OutQuint);
                rightArrow?.FadeTo(showArrows ? 1 : 0, 120, Easing.OutQuint);
                stableSprite?.ScaleTo(showArrows ? 1.06f : 1.0f, 150, Easing.OutElastic);
                if (showArrows)
                    hoverSample?.Play();
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (themeBindable?.Value.IsThemed() ?? false)
                    clickSample?.Play();
                return base.OnClick(e);
            }

            // required to ensure keyboard navigation always starts from an extremity (unless the cursor is moved)
            protected override bool OnHover(HoverEvent e) => true;

            protected override bool OnMouseMove(MouseMoveEvent e)
            {
                State = SelectionState.Selected;
                return base.OnMouseMove(e);
            }
        }

        protected override bool Handle(UIEvent e)
        {
            switch (e)
            {
                case ScrollEvent:
                    if (ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                        return globalAction.TriggerEvent(e);

                    break;
            }

            return base.Handle(e);
        }

        #region Pause loop sound handling

        public override bool IsPresent => base.IsPresent || pauseLoop.IsPlaying;

        private SkinnableSound pauseLoop = null!;

        private readonly IBindable<bool> windowActive = new Bindable<bool>(true);

        private float targetVolume => windowActive.Value && State.Value == Visibility.Visible ? 1.0f : 0;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Schedule required because host.IsActive doesn't seem to always run on the update thread.
            windowActive.BindValueChanged(_ => Schedule(() => pauseLoop.VolumeTo(targetVolume, 1000, Easing.Out)));
        }

        public void StopAllSamples()
        {
            if (!IsLoaded)
                return;

            pauseLoop.Stop();
        }

        private void startPauseLoop()
        {
            pauseLoop.VolumeTo(targetVolume, TRANSITION_DURATION, Easing.InQuint);
            pauseLoop.Play();
        }

        private void stopPauseLoop()
        {
            pauseLoop.VolumeTo(targetVolume, TRANSITION_DURATION, Easing.OutQuad).Finally(_ => pauseLoop.Stop());
        }

        #endregion
    }
}
