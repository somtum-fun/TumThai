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
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play;
using osu.Game.Screens.RankingV2.Legacy;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// TumThai!Next: osu!stable two-column Ranking / Results screen layout.
    /// </summary>
    public partial class StableResultsContent : CompositeDrawable
    {
        public readonly Bindable<ScoreInfo?> SelectedScore = new Bindable<ScoreInfo?>();

        public bool AllowRetry { get; init; }
        public bool AllowWatchingReplay { get; init; }

        /// <summary>
        /// The score the player just set and the server accepted. Only then is there an Online Ranking page
        /// ("You achieved..."): never for replays or scores opened from a leaderboard.
        /// </summary>
        public ScoreInfo? SubmittedScore { get; init; }
        public Action? RequestToggleScoreboard { get; init; }
        public Action? RequestExit { get; init; }

        [Cached(typeof(IBindable<IScoreInfo>))]
        private readonly Bindable<IScoreInfo> scoreBindable = new Bindable<IScoreInfo>();

        private OsuSpriteText titleText = null!;
        private OsuSpriteText mapperText = null!;
        private OsuSpriteText playedByText = null!;
        private Container rankingTitleContainer = null!;
        private LegacyRankingPanel rankingPanel = null!;
        private Sprite rankingGraphSprite = null!;
        private Container perfectBadgeContainer = null!;
        private Sprite gradeSprite = null!;
        private Sprite gradePulseSprite = null!;
        private FillFlowContainer modsFlow = null!;
        private FillFlowContainer performanceBarsFlow = null!;
        private Container page1Container = null!;
        private StableRankingDialog rankingDialog = null!;
        private StableSpriteActionButton? retryButton;
        private StableSpriteActionButton? replayButton;
        private bool isShowingDialog;

        private ScoreDownloadTracker? downloadTracker;
        private readonly Bindable<DownloadState> replayDownloadState = new Bindable<DownloadState>();
        private IBindable<Skin> currentSkin = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private Player? player { get; set; }

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        [Resolved(canBeNull: true)]
        private ScoreModelDownloader? scoreDownloader { get; set; }

        public StableResultsContent()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var classicSkin = skinManager.DefaultClassicSkin;
            ISkin hudSkin = new ThemeHudSkin(classicSkin, skinManager);

            // Ensure scoreBindable has a valid initial ScoreInfo before LegacyRankingPanel loads.
            scoreBindable.Value = SelectedScore.Value ?? new ScoreInfo();

            FillFlowContainer rightActionButtons;

            InternalChild = new SkinProvidingContainer(hudSkin)
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // Page 1: Left & Right main content scaled relative to 768p height (scrolls up when Online Ranking dialog opens)
                    page1Container = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            // Rotating sunburst behind grade on the right
                            new LegacyRankingBackgroundOverlay
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.Centre,
                                Position = new Vector2(-288f, 310f),
                            },
                            // Left ranking panel
                            new Container
                            {
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopLeft,
                                Position = new Vector2(0f, 96f),
                                Scale = new Vector2(0.92f),
                                AutoSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    rankingPanel = new LegacyRankingPanel(),
                                    // ranking-graph box at (248, 505) with HP/performance curve inside
                                    new Container
                                    {
                                        Position = new Vector2(248f, 505f),
                                        AutoSizeAxes = Axes.Both,
                                        Children = new Drawable[]
                                        {
                                            rankingGraphSprite = new Sprite
                                            {
                                                Texture = hudSkin.GetTexture(@"ranking-graph"),
                                            },
                                            new Container
                                            {
                                                Position = new Vector2(12f, 28f),
                                                Size = new Vector2(292f, 104f),
                                                Masking = true,
                                                Child = performanceBarsFlow = new FillFlowContainer
                                                {
                                                    Anchor = Anchor.BottomLeft,
                                                    Origin = Anchor.BottomLeft,
                                                    RelativeSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Horizontal,
                                                    Spacing = new Vector2(1f, 0f),
                                                },
                                            },
                                        },
                                    },
                                    // ranking-perfect at (260, 430) in 480p -> (416, 592) relative to Y=96 (Ranking.cs line 513)
                                    perfectBadgeContainer = new Container
                                    {
                                        Origin = Anchor.Centre,
                                        Position = new Vector2(416f, 592f),
                                        Scale = new Vector2(0.56f),
                                        AutoSizeAxes = Axes.Both,
                                        Alpha = 0f,
                                    },
                                },
                            },
                            // Right side: Grade letter, mods, Retry / Watch Replay buttons
                            new Container
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                RelativeSizeAxes = Axes.Y,
                                Width = 480f,
                                Children = new Drawable[]
                                {
                                    gradePulseSprite = new Sprite
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.Centre,
                                        Position = new Vector2(-200f, 320f),
                                        Blending = BlendingParameters.Additive,
                                        Alpha = 0f,
                                    },
                                    gradeSprite = new Sprite
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.Centre,
                                        Position = new Vector2(-200f, 320f),
                                    },
                                    modsFlow = new FillFlowContainer
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.CentreRight,
                                        Position = new Vector2(-48f, 430f),
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(-12f, 0f),
                                    },
                                    rightActionButtons = new FillFlowContainer
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        Position = new Vector2(0f, 500f),
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0f, 14f),
                                    },
                                },
                            },
                            // Bottom-center osu!stable "▼ Online Ranking ▼" button (Ranking.cs lines 785-792)
                            new StableThreeSliceButton
                            {
                                Alpha = SubmittedScore != null ? 1 : 0,
                                Anchor = Anchor.BottomCentre,
                                Origin = Anchor.BottomCentre,
                                Y = -6f,
                                Size = new Vector2(280f, 36f),
                                BackgroundColour = Color4.BlueViolet,
                                Text = "▼ Online Ranking ▼",
                                FontSize = 18f,
                                Action = () => showRankingDialog(true),
                            },
                        },
                    },
                    // Page 2: osu!stable after-play Ranking Chart / Score Submission dialog (LOCAL / ONLINE / BEATMAP)
                    rankingDialog = new StableRankingDialog(
                        () => showRankingDialog(false),
                        () =>
                        {
                            showRankingDialog(false);
                            RequestToggleScoreboard?.Invoke();
                        }),
                    // Bottom-left osu!stable back button (ViewOffsetImmune in Ranking.cs, stays above Page 1 & Page 2)
                    new StableBackButton
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Action = () => RequestExit?.Invoke(),
                    },
                    // Top header bar (96px tall, Black 82% alpha) kept in front of both Page 1 and Page 2
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 96f,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.Black.Opacity(0.82f),
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopLeft,
                                Position = new Vector2(10f, 4f),
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0f, 2f),
                                Children = new Drawable[]
                                {
                                    titleText = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 26, weight: FontWeight.SemiBold),
                                        Colour = Color4.White,
                                    },
                                    mapperText = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 18),
                                        Colour = Color4.White,
                                    },
                                    playedByText = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 18),
                                        Colour = Color4.White,
                                    },
                                },
                            },
                            rankingTitleContainer = new Container
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Position = new Vector2(-24f, 4f),
                                AutoSizeAxes = Axes.Both,
                                Scale = new Vector2(0.78f),
                            },
                        },
                    },
                },
            };

            if (player != null && AllowRetry)
            {
                rightActionButtons.Add(retryButton = new StableSpriteActionButton(
                    @"pause-retry",
                    "Retry",
                    Color4Extensions.FromHex("E96800"))
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Action = () => player.Restart(),
                });
            }

            if (AllowWatchingReplay)
            {
                rightActionButtons.Add(replayButton = new StableSpriteActionButton(
                    @"pause-replay",
                    "Watch Replay",
                    Color4Extensions.FromHex("0096EC"))
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Action = triggerWatchReplay,
                });
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentSkin = skinManager.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ =>
            {
                rankingTitleContainer.Clear();
                var titleAnim = skinManager.GetClassicAnimation(@"ranking-title");
                if (titleAnim != null)
                    rankingTitleContainer.Add(titleAnim);

                rankingGraphSprite.Texture = skinManager.GetClassicTexture(@"ranking-graph");

                perfectBadgeContainer.Clear();
                var perfectAnim = skinManager.GetClassicAnimation(@"ranking-perfect");
                if (perfectAnim != null)
                {
                    perfectAnim.Anchor = Anchor.Centre;
                    perfectAnim.Origin = Anchor.Centre;
                    perfectBadgeContainer.Add(perfectAnim);
                }

                retryButton?.UpdateSkin(skinManager);
                replayButton?.UpdateSkin(skinManager);
                if (SelectedScore.Value != null)
                    onScoreChanged(new ValueChangedEvent<ScoreInfo?>(SelectedScore.Value, SelectedScore.Value));
            }, true);

            SelectedScore.BindValueChanged(onScoreChanged, true);

            if (SubmittedScore != null)
            {
                rankingDialog.SetScore(SubmittedScore);
                Scheduler.AddDelayed(() => showRankingDialog(true), 1200);
            }
        }

        private void showRankingDialog(bool show)
        {
            if (isShowingDialog == show)
                return;

            if (show && SubmittedScore == null)
                return;

            isShowingDialog = show;

            if (show)
            {
                page1Container.MoveToY(-672f, 450, Easing.OutQuint).FadeOut(300, Easing.OutQuint);
                rankingDialog.Show();
            }
            else
            {
                page1Container.MoveToY(0f, 450, Easing.OutQuint).FadeIn(300, Easing.OutQuint);
                rankingDialog.Hide();
            }
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (e.ScrollDelta.Y < 0 && !isShowingDialog && SubmittedScore != null)
            {
                showRankingDialog(true);
                return true;
            }

            if (e.ScrollDelta.Y > 0 && isShowingDialog)
            {
                showRankingDialog(false);
                return true;
            }

            return base.OnScroll(e);
        }

        private void onScoreChanged(ValueChangedEvent<ScoreInfo?> e)
        {
            var score = e.NewValue;
            if (score == null)
                return;

            scoreBindable.Value = score;

            downloadTracker?.RemoveAndDisposeImmediately();
            downloadTracker = null;
            replayDownloadState.SetDefault();

            AddInternal(downloadTracker = new ScoreDownloadTracker(score)
            {
                State = { BindTarget = replayDownloadState }
            });

            var beatmap = score.BeatmapInfo;
            var metadata = beatmap?.Metadata;

            string artist = metadata?.Artist ?? string.Empty;
            string title = metadata?.Title ?? string.Empty;
            string diff = beatmap?.DifficultyName ?? string.Empty;
            string creator = metadata?.Author.Username ?? string.Empty;

            titleText.Text = !string.IsNullOrEmpty(artist)
                ? $"{artist} - {title} [{diff}]"
                : $"{title} [{diff}]";
            mapperText.Text = $"Beatmap by {creator}";
            playedByText.Text = $"Played by {score.User.Username} on {score.Date.ToLocalTime():yyyy-MM-dd HH:mm:ss}.";

            int missCount = score.GetCountMiss() ?? 0;
            bool isFullCombo = score.Passed && missCount == 0 && (score.Statistics.GetValueOrDefault(HitResult.LargeTickMiss) == 0);
            perfectBadgeContainer.Alpha = isFullCombo ? 1f : 0f;

            var classicSkin = skinManager.DefaultClassicSkin;
            ISkin hudSkin = new ThemeHudSkin(classicSkin, skinManager);
            string rankName = score.Rank switch
            {
                ScoreRank.XH => @"ranking-XH",
                ScoreRank.X => @"ranking-X",
                ScoreRank.SH => @"ranking-SH",
                ScoreRank.S => @"ranking-S",
                ScoreRank.A => @"ranking-A",
                ScoreRank.B => @"ranking-B",
                ScoreRank.C => @"ranking-C",
                _ => @"ranking-D",
            };

            var rankTex = hudSkin.GetTexture(rankName);
            gradeSprite.Texture = rankTex;
            gradePulseSprite.Texture = rankTex;

            gradeSprite.FinishTransforms();
            gradeSprite.ScaleTo(1.5f).FadeOut().Then().ScaleTo(0.85f, 800, Easing.OutQuint).FadeIn(400, Easing.OutQuint);

            gradePulseSprite.FinishTransforms();
            if (score.Rank >= ScoreRank.B && rankTex != null)
            {
                gradePulseSprite.ScaleTo(0.85f).FadeTo(0.4f).Then().ScaleTo(1.0f, 1200, Easing.OutQuad).FadeOut(1200, Easing.OutQuad);
            }

            modsFlow.Clear();
            foreach (var mod in score.Mods)
            {
                string? modTexName = StableModSelectContent.GetStableModTextureName(mod.Acronym);
                var modTex = modTexName != null ? hudSkin.GetTexture(modTexName) : null;
                if (modTex != null)
                {
                    modsFlow.Add(new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Texture = modTex,
                        Scale = new Vector2(0.85f),
                    });
                }
                else
                {
                    modsFlow.Add(new ModIcon(mod)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Scale = new Vector2(0.65f),
                    });
                }
            }

            populatePerformanceGraph(score);
            rankingPanel.StartAnimating(Time.Current);
        }

        private void populatePerformanceGraph(ScoreInfo score)
        {
            performanceBarsFlow.Clear();
            const int columns = 48;
            double acc = Math.Clamp(score.Accuracy, 0.2, 1.0);
            int misses = score.GetCountMiss() ?? 0;

            for (int i = 0; i < columns; i++)
            {
                float progress = (float)i / (columns - 1);
                float wave = 0.08f * MathF.Sin(progress * 11f) + 0.05f * MathF.Cos(progress * 23f);
                float dip = (misses > 0 && i == columns / 2) ? -0.35f : 0f;
                float heightRatio = Math.Clamp((float)acc * (0.75f + 0.22f * progress) + wave + dip, 0.15f, 0.96f);

                Color4 barColour = heightRatio < 0.45f
                    ? new Color4(235, 65, 45, 220)
                    : new Color4(195, 240, 55, 220);

                performanceBarsFlow.Add(new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Width = 5f,
                    RelativeSizeAxes = Axes.Y,
                    Height = heightRatio,
                    Colour = barColour,
                });
            }
        }

        private void triggerWatchReplay()
        {
            var score = SelectedScore.Value;
            if (score == null)
                return;

            if (replayDownloadState.Value == DownloadState.LocallyAvailable)
                game?.PresentScore(score, ScorePresentType.Gameplay);
            else if (replayDownloadState.Value == DownloadState.NotDownloaded && score.HasOnlineReplay)
                scoreDownloader?.Download(score);
            else
                game?.PresentScore(score, ScorePresentType.Gameplay);
        }

        private partial class StableSpriteActionButton : OsuClickableContainer
        {
            private readonly string textureName;
            private readonly string fallbackText;
            private readonly Color4 fallbackColour;
            private Sprite? sprite;
            private ISample? hoverSample;
            private ISample? clickSample;

            public StableSpriteActionButton(string textureName, string fallbackText, Color4 fallbackColour)
            {
                this.textureName = textureName;
                this.fallbackText = fallbackText;
                this.fallbackColour = fallbackColour;

                AutoSizeAxes = Axes.Both;
                Alpha = 0.78f;
            }

            [BackgroundDependencyLoader]
            private void load(SkinManager skinManager, AudioManager audio)
            {
                hoverSample = skinManager.GetClassicSample(@"menuclick") ?? audio.Samples.Get(@"Menu/button-hover");
                clickSample = skinManager.GetClassicSample(@"menuhit") ?? audio.Samples.Get(@"UI/button-select");

                var texture = skinManager.GetClassicTexture(textureName);
                if (texture != null)
                {
                    Child = sprite = new Sprite
                    {
                        Texture = texture,
                        Scale = new Vector2(0.85f),
                    };
                }
                else
                {
                    Child = new StableThreeSliceButton
                    {
                        Size = new Vector2(210f, 52f),
                        Text = fallbackText,
                        BackgroundColour = fallbackColour,
                    };
                }
            }

            public void UpdateSkin(SkinManager skinManager)
            {
                hoverSample = skinManager.GetClassicSample(@"menuclick");
                clickSample = skinManager.GetClassicSample(@"menuhit");
                var tex = skinManager.GetClassicTexture(textureName);
                if (sprite != null && tex != null)
                    sprite.Texture = tex;
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSample?.Play();
                this.FadeTo(1f, 150, Easing.OutQuad);
                this.ScaleTo(1.05f, 150, Easing.OutQuad);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                this.FadeTo(0.78f, 150, Easing.OutQuad);
                this.ScaleTo(1f, 150, Easing.OutQuad);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (Enabled.Value)
                    clickSample?.Play();

                return base.OnClick(e);
            }
        }
    }
}
