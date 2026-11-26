// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.Placeholders;
using osu.Game.Overlays;
using osu.Game.Performance;
using osu.Game.Scoring;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    [Cached]
    public abstract partial class ResultsScreen : ScreenWithBeatmapBackground, IKeyBindingHandler<GlobalAction>
    {
        protected const float BACKGROUND_BLUR = 10;
        private static readonly float screen_height = 768 - TwoLayerButton.SIZE_EXTENDED.Y;

        public override bool DisallowExternalBeatmapRulesetChanges => true;

        public override bool? AllowGlobalTrackControl => true;

        protected override OverlayActivation InitialOverlayActivationMode => OverlayActivation.UserTriggered;

        public readonly Bindable<ScoreInfo?> SelectedScore = new Bindable<ScoreInfo?>();

        public readonly ScoreInfo? Score;

        protected ScorePanelList ScorePanelList { get; private set; } = null!;

        protected VerticalScrollContainer VerticalScrollContent { get; private set; } = null!;

        [Resolved]
        private Player? player { get; set; }

        private bool skipExitTransition;

        protected StatisticsPanel StatisticsPanel { get; private set; } = null!;

        private Drawable bottomPanel = null!;
        private Container<ScorePanel> detachedPanelContainer = null!;

        private Task lastFetchTask = Task.CompletedTask;

        /// <summary>
        /// Whether the user can retry the beatmap from the results screen.
        /// </summary>
        public bool AllowRetry { get; init; }

        /// <summary>
        /// Whether the user can watch the replay of the completed play from the results screen.
        /// </summary>
        public bool AllowWatchingReplay { get; init; } = true;

        /// <summary>
        /// Whether the provided score is for a local user's play.
        /// This will trigger elements like the user's ranking to display.
        /// </summary>
        public bool IsLocalPlay { get; init; }

        private Sample? popInSample;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private readonly Bindable<InterfaceTheme> interfaceTheme = new Bindable<InterfaceTheme>();

        // TumThai: the V2 results screen (ported from torii, MIT), picked in settings (ResultsScreenStyle).
        public readonly BindableBool IsPersonalBest = new BindableBool();
        public readonly Bindable<ScoreInfo?> ComparisonScore = new Bindable<ScoreInfo?>();
        public readonly BindableBool V2DetailsVisible = new BindableBool();
        public readonly BindableBool V2LeaderboardVisible = new BindableBool();
        private readonly Bindable<ResultsScreenStyle> resultsStyle = new Bindable<ResultsScreenStyle>();
        private Container v2Layer = null!;
        private Container v2PanelContainer = null!;
        private Container v2FooterContainer = null!;
        private V2ResultsFooter v2Footer = null!;
        private bool v2PanelLoading;
        private bool v2IntroShown;
        private V2RankingTable? rankingTable;
        private V2ResultsPanel? rev11Details;
        private bool? lastV2Background;
        private bool hasEntered;
        private Box bottomBarBackground = null!;
        private GridContainer lazerResultsGrid = null!;
        private StableResultsContent stableResultsContent = null!;
        private bool showingStableScoreboard;

        protected ResultsScreen(ScoreInfo? score)
        {
            Score = score;

            SelectedScore.Value = score;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, OsuConfigManager config)
        {
            FillFlowContainer buttons;

            config.BindWith(OsuSetting.InterfaceTheme, interfaceTheme);
            config.BindWith(OsuSetting.ResultsScreenStyle, resultsStyle);

            popInSample = audio.Samples.Get(@"UI/overlay-pop-in");

            InternalChild = new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    lazerResultsGrid = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                VerticalScrollContent = new VerticalScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    ScrollbarVisible = false,
                                    Child = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Children = new Drawable[]
                                        {
                                            StatisticsPanel = new StatisticsPanel
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Score = { BindTarget = SelectedScore },
                                                AchievedScore = IsLocalPlay && Score != null ? Score : null,
                                            },
                                            ScorePanelList = new ScorePanelList
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                SelectedScore = { BindTarget = SelectedScore },
                                                PostExpandAction = () => StatisticsPanel.ToggleVisibility()
                                            },
                                            detachedPanelContainer = new Container<ScorePanel>
                                            {
                                                RelativeSizeAxes = Axes.Both
                                            },
                                        }
                                    }
                                },
                            },
                            new[]
                            {
                                bottomPanel = new Container
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    RelativeSizeAxes = Axes.X,
                                    Height = TwoLayerButton.SIZE_EXTENDED.Y,
                                    Alpha = 0,
                                    Children = new Drawable[]
                                    {
                                        bottomBarBackground = new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = interfaceTheme.Value.Effective().ResultsBottomBar()
                                        },
                                        buttons = new FillFlowContainer
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            AutoSizeAxes = Axes.Both,
                                            Spacing = new Vector2(5),
                                            Direction = FillDirection.Horizontal
                                        },
                                    }
                                }
                            }
                        },
                        RowDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize)
                        }
                    },
                    stableResultsContent = new StableResultsContent
                    {
                        SelectedScore = { BindTarget = SelectedScore },
                        AllowRetry = AllowRetry,
                        AllowWatchingReplay = AllowWatchingReplay,
                        // The score's online id is only set once the server accepted it (SubmittingPlayer.submitScore).
                        SubmittedScore = IsLocalPlay && Score?.OnlineID > 0 ? Score : null,
                        Alpha = 0,
                        RequestExit = () => this.Exit(),
                        RequestToggleScoreboard = () =>
                        {
                            showingStableScoreboard = true;
                            updateInterfaceThemeView();
                        },
                    },
                    v2Layer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            // depth: the panel at the back, then the ranking table, the footer, an intro in front
                            v2PanelContainer = new Container { RelativeSizeAxes = Axes.Both, Depth = 2 },
                            v2FooterContainer = new Container
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                RelativeSizeAxes = Axes.X,
                                Child = v2Footer = new V2ResultsFooter
                                {
                                    Rev11Layout = resultsStyle.Value == ResultsScreenStyle.Rev11,
                                    Score = Score,
                                    AllowWatchingReplay = AllowWatchingReplay,
                                    BackAction = () =>
                                    {
                                        if (!OnBackButton())
                                            this.Exit();
                                    },
                                    RankingAction = toggleRankingTable,
                                    DetailsAction = () =>
                                    {
                                        if (resultsStyle.Value == ResultsScreenStyle.Rev11)
                                        {
                                            toggleRev11Details();
                                            return;
                                        }

                                        foreach (var panel in v2Panels)
                                            panel.ToggleDetails();
                                    },
                                    RetryAction = player != null && AllowRetry
                                        ? () =>
                                        {
                                            if (!this.IsCurrentScreen()) return;

                                            skipExitTransition = true;
                                            player?.Restart(true);
                                        }
                                        : null,
                                },
                            },
                        },
                    },
                }
            };

            if (Score != null)
            {
                // only show flair / animation when arriving after watching a play that isn't autoplay.
                ScorePanelList.AddScore(Score, shouldFlair && !useV2Layout);
                // this is mostly for medal display.
                // we don't want the medal animation to trample on the results screen animation, so we (ab)use `OverlayActivationMode`
                // to give the results screen enough time to play the animation out before the medals can be shown.
                Scheduler.AddDelayed(() => OverlayActivationMode.Value = OverlayActivation.All, shouldFlair ? AccuracyCircle.TOTAL_DURATION + 1000 : 0);
            }

            bool allowHotkeyRetry = false;

            if (AllowWatchingReplay)
            {
                buttons.Add(new ReplayDownloadButton(SelectedScore.Value)
                {
                    Score = { BindTarget = SelectedScore },
                    Width = 300
                });

                // for simplicity, only allow this when coming from a replay player where we know the replay is ready to be played.
                //
                // if we show it in all cases, consider the case where a user comes from song select and potentially has to download
                // the replay before it can be played back. it wouldn't flow well with the quick retry in such a case.
                allowHotkeyRetry = player is ReplayPlayer;
            }

            if (player != null && AllowRetry)
            {
                buttons.Add(new RetryButton { Width = 300 });
                allowHotkeyRetry = true;
            }

            if (allowHotkeyRetry)
            {
                AddInternal(new HotkeyRetryOverlay
                {
                    Action = () =>
                    {
                        if (!this.IsCurrentScreen()) return;

                        skipExitTransition = true;
                        player?.Restart(true);
                    },
                });
            }

            if (Score?.BeatmapInfo != null)
                buttons.Add(new CollectionButton(Score.BeatmapInfo));

            if (Score?.BeatmapInfo?.BeatmapSet != null && Score.BeatmapInfo.BeatmapSet.OnlineID > 0)
                buttons.Add(new FavouriteButton(Score.BeatmapInfo.BeatmapSet));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            v2Footer.DetailsVisible.BindTo(V2DetailsVisible);
            v2Footer.LeaderboardVisible.BindTo(V2LeaderboardVisible);

            interfaceTheme.BindValueChanged(t =>
            {
                bottomBarBackground.Colour = t.NewValue.Effective().ResultsBottomBar();
                showingStableScoreboard = false;
                updateInterfaceThemeView();
            }, true);
            resultsStyle.BindValueChanged(_ =>
            {
                showingStableScoreboard = false;
                updateInterfaceThemeView();
            });
            SelectedScore.BindValueChanged(_ => updateInterfaceThemeView());

            StatisticsPanel.State.BindValueChanged(onStatisticsStateChanged, true);

            fetchScores(null);
        }

        /// <summary>
        /// TumThai: the Rev 11 style's More Info: the advanced metrics (graphs, timing distribution, heatmap) in place of the
        /// score overview, and back.
        /// </summary>
        private void toggleRev11Details()
        {
            if (Score == null)
                return;

            var overview = v2PanelContainer.Children.OfType<Rev11ResultsPanel>().FirstOrDefault();

            if (rev11Details == null)
            {
                rev11Details = new V2ResultsPanel(Score) { Alpha = 0 };
                v2PanelContainer.Add(rev11Details);
                rev11Details.OnLoadComplete += _ =>
                {
                    rev11Details?.ShowDetails();
                    rev11Details?.FadeIn(200, Easing.OutQuint);
                };
                overview?.FadeOut(200, Easing.OutQuint);
            }
            else
            {
                var details = rev11Details;
                rev11Details = null;
                details.CloseDetails();
                details.FadeOut(200, Easing.OutQuint).Expire();
                overview?.FadeIn(200, Easing.OutQuint);
            }
        }

        /// <summary>TumThai: the beatmap's full ranking (tabs, mods filter, table) over the modern layouts.</summary>
        private void toggleRankingTable()
        {
            if (Score == null)
                return;

            if (rankingTable == null)
                v2Layer.Add(rankingTable = new V2RankingTable(Score) { Depth = 1 });

            rankingTable.ToggleVisibility();
        }

        private IEnumerable<V2ResultsPanel> v2Panels => v2PanelContainer.Children.OfType<V2ResultsPanel>();

        /// <summary>
        /// TumThai: the V2 layout is for a solo play's own results (it needs the beatmap); other results screens keep theirs.
        /// </summary>
        private bool useV2Layout => resultsStyle.Value is ResultsScreenStyle.V2 or ResultsScreenStyle.Spotlight or ResultsScreenStyle.Rev11 && this is SoloResultsScreen && Score?.BeatmapInfo != null;

        /// <summary>The panel of the chosen modern layout for <paramref name="score"/>.</summary>
        private Drawable createV2Panel(ScoreInfo score, bool withFlair = false) => resultsStyle.Value switch
        {
            ResultsScreenStyle.Spotlight => new SpotlightResultsPanel(score, withFlair),
            ResultsScreenStyle.Rev11 => new Rev11ResultsPanel(score, withFlair),
            _ => new V2ResultsPanel(score, withFlair),
        };

        private void updateInterfaceThemeView()
        {
            bool v2 = useV2Layout;
            bool stableStyle = resultsStyle.Value == ResultsScreenStyle.Stable
                               || (resultsStyle.Value == ResultsScreenStyle.Auto && interfaceTheme.Value.IsThemed());
            bool useStableLayout = !v2 && stableStyle && !showingStableScoreboard && SelectedScore.Value != null;

            stableResultsContent.Alpha = useStableLayout ? 1f : 0f;
            lazerResultsGrid.Alpha = useStableLayout || v2 ? 0f : 1f;
            v2Layer.Alpha = v2 ? 1f : 0f;
            FooterContent.Value = useStableLayout || v2 ? ScreenFooterContent.None : ScreenFooterContent.Legacy(showBackButton: AllowUserExit);

            // A different modern layout picked while showing: start over with it.
            if (v2 && v2PanelContainer.Count > 0 && v2PanelContainer[0].GetType() != createV2Panel(Score!).GetType())
                v2PanelContainer.Clear();

            if (v2 && v2PanelContainer.Count == 0 && !v2PanelLoading && Score != null)
            {
                v2PanelLoading = true;

                // Right after a play: "FULL COMBO!" / the grade, big, before the results (once). The results come in,
                // and start their own animation, as it steps aside.
                bool introGone = true;
                Drawable? loaded = null;

                void addPanel()
                {
                    if (loaded == null || !introGone) return;

                    v2PanelContainer.Add(loaded);
                    v2PanelLoading = false;
                    loaded = null;
                }

                if (shouldFlair && !v2IntroShown)
                {
                    v2IntroShown = true;
                    introGone = false;
                    v2Layer.Add(new V2ResultsIntro(Score)
                    {
                        Rev11Colours = resultsStyle.Value == ResultsScreenStyle.Rev11,
                        Revealing = () =>
                        {
                            introGone = true;
                            addPanel();
                        },
                    });
                }

                LoadComponentAsync(createV2Panel(Score, shouldFlair), panel =>
                {
                    loaded = panel;
                    addPanel();
                });
            }

            // The V2 layout shows the background unblurred (set on entering; here only once the screen is showing).
            if (hasEntered && this.IsCurrentScreen() && lastV2Background != v2)
            {
                lastV2Background = v2;
                ApplyToBackground(b => b.BlurAmount.Value = v2 ? 0 : BACKGROUND_BLUR);
            }
        }

        private bool shouldFlair => !PotatoMode.UltraActive && player != null && Score?.User.IsBot == false;

        public IEnumerable<ScoreInfo> GetOverviewScores() => ScorePanelList.GetScorePanels().Select(p => p.Score);

        public void SelectOverviewScore(ScoreInfo score)
        {
            Schedule(() =>
            {
                V2LeaderboardVisible.Value = false;
                V2DetailsVisible.Value = false;
                SelectedScore.Value = score;
                v2PanelContainer.Clear();
                v2PanelContainer.Add(createV2Panel(score));
            });
        }

        protected override void Update()
        {
            base.Update();

            // The V2 footer is laid out for a 1280-wide screen and scales with the width.
            v2FooterContainer.Height = DrawWidth * V2ResultsFooter.DESIGN_HEIGHT / 1280;

            if (ScorePanelList.IsScrolledToStart)
                fetchScores(-1);
            else if (ScorePanelList.IsScrolledToEnd)
                fetchScores(1);
        }

        #region Applause

        private PoolableSkinnableSample? rankApplauseSound;

        public void PlayApplause(ScoreRank rank)
        {
            const double applause_volume = 0.8f;

            if (!this.IsCurrentScreen())
                return;

            rankApplauseSound?.Dispose();

            var applauseSamples = new List<string>();

            if (rank >= ScoreRank.B)
                // when rank is B or higher, play legacy applause sample on legacy skins.
                applauseSamples.Insert(0, @"applause");

            switch (rank)
            {
                default:
                case ScoreRank.D:
                    applauseSamples.Add(@"Results/applause-d");
                    break;

                case ScoreRank.C:
                    applauseSamples.Add(@"Results/applause-c");
                    break;

                case ScoreRank.B:
                    applauseSamples.Add(@"Results/applause-b");
                    break;

                case ScoreRank.A:
                    applauseSamples.Add(@"Results/applause-a");
                    break;

                case ScoreRank.S:
                case ScoreRank.SH:
                case ScoreRank.X:
                case ScoreRank.XH:
                    applauseSamples.Add(@"Results/applause-s");
                    break;
            }

            LoadComponentAsync(rankApplauseSound = new PoolableSkinnableSample(new SampleInfo(applauseSamples.ToArray())), s =>
            {
                if (!this.IsCurrentScreen() || s != rankApplauseSound)
                    return;

                AddInternal(rankApplauseSound);

                rankApplauseSound.VolumeTo(applause_volume);
                rankApplauseSound.Play();
            });
        }

        #endregion

        /// <summary>
        /// Fetches the next page of scores in the given direction.
        /// </summary>
        /// <param name="direction">The direction, or <c>null</c> to fetch any scores.</param>
        private void fetchScores(int? direction)
        {
            Debug.Assert(direction == null || direction == -1 || direction == 1);

            if (!lastFetchTask.IsCompleted)
                return;

            lastFetchTask = Task.Run(async () =>
            {
                ScoreInfo[] scores;

                switch (direction)
                {
                    default:
                        scores = await FetchScores().ConfigureAwait(false);
                        break;

                    case -1:
                    case 1:
                        scores = await FetchNextPage(direction.Value).ConfigureAwait(false);
                        break;
                }

                await addScores(scores).ConfigureAwait(false);
            });
        }

        /// <summary>
        /// Performs a fetch/refresh of scores to be displayed.
        /// </summary>
        protected virtual Task<ScoreInfo[]> FetchScores() => Task.FromResult<ScoreInfo[]>([]);

        /// <summary>
        /// Performs a fetch of the next page of scores. This is invoked every frame.
        /// </summary>
        /// <param name="direction">The fetch direction. -1 to fetch scores greater than the current start of the list, and 1 to fetch scores lower than the current end of the list.</param>
        protected virtual Task<ScoreInfo[]> FetchNextPage(int direction) => Task.FromResult<ScoreInfo[]>([]);

        private Task addScores(ScoreInfo[] scores)
        {
            var tcs = new TaskCompletionSource();

            Schedule(() =>
            {
                foreach (var s in scores)
                {
                    var panel = ScorePanelList.AddScore(s);
                    if (detachedPanel != null)
                        panel.Alpha = 0;
                }

                // allow a frame for scroll container to adjust its dimensions with the added scores before fetching again.
                Schedule(tcs.SetResult);

                if (ScorePanelList.IsEmpty)
                {
                    // This can happen if for example a beatmap that is part of a playlist hasn't been played yet.
                    VerticalScrollContent.Add(new MessagePlaceholder(LeaderboardStrings.NoRecordsYet));
                }

                OnScoresAdded(scores);
            });

            return tcs.Task;
        }

        /// <summary>
        /// Invoked after online scores are fetched and added to the list.
        /// </summary>
        /// <param name="scores">The scores that were added.</param>
        protected virtual void OnScoresAdded(ScoreInfo[] scores)
        {
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            ApplyToBackground(b =>
            {
                b.BlurAmount.Value = useV2Layout ? 0 : BACKGROUND_BLUR;
                b.FadeColour(OsuColour.Gray(0.5f), PotatoMode.UltraActive ? 0 : 250);
            });

            hasEntered = true;
            lastV2Background = useV2Layout;

            bottomPanel.FadeTo(1, PotatoMode.UltraActive ? 0 : 250);

            popInSample?.Play();
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (base.OnExiting(e))
                return true;

            // This is a stop-gap safety against components holding references to gameplay after exiting the gameplay flow.
            // Right now, HitEvents are only used up to the results screen. If this changes in the future we need to remove
            // HitObject references from HitEvent.
            Score?.HitEvents.Clear();

            if (!skipExitTransition)
                this.FadeOut(PotatoMode.UltraActive ? 0 : 100);

            rankApplauseSound?.Stop();
            return false;
        }

        public override bool OnBackButton()
        {
            if (rev11Details != null)
            {
                toggleRev11Details();
                return true;
            }

            if (rankingTable?.State.Value == Visibility.Visible)
            {
                rankingTable.Hide();
                return true;
            }

            if (useV2Layout && v2Panels.Any(p => p.CloseLeaderboard() || p.CloseDetails()))
                return true;

            if (StatisticsPanel.State.Value == Visibility.Visible)
            {
                StatisticsPanel.Hide();
                return true;
            }

            if (interfaceTheme.Value.IsThemed() && showingStableScoreboard)
            {
                showingStableScoreboard = false;
                updateInterfaceThemeView();
                return true;
            }

            return false;
        }

        private ScorePanel? detachedPanel;

        private void onStatisticsStateChanged(ValueChangedEvent<Visibility> state)
        {
            if (state.NewValue == Visibility.Visible)
            {
                Debug.Assert(SelectedScore.Value != null);
                // Detach the panel in its original location, and move into the desired location in the local container.
                var expandedPanel = ScorePanelList.GetPanelForScore(SelectedScore.Value);
                var screenSpacePos = expandedPanel.ScreenSpaceDrawQuad.TopLeft;

                // Detach and move into the local container.
                ScorePanelList.Detach(expandedPanel);
                detachedPanelContainer.Add(expandedPanel);

                // Move into its original location in the local container first, then to the final location.
                float origLocation = detachedPanelContainer.ToLocalSpace(screenSpacePos).X;
                expandedPanel.MoveToX(origLocation)
                             .Then()
                             .MoveToX(StatisticsPanel.SIDE_PADDING, PotatoMode.UltraActive ? 0 : 400, Easing.OutElasticQuarter);

                // Hide contracted panels.
                foreach (var contracted in ScorePanelList.GetScorePanels().Where(p => p.State == PanelState.Contracted))
                    contracted.FadeOut(PotatoMode.UltraActive ? 0 : 150, Easing.OutQuint);
                ScorePanelList.HandleInput = false;

                // Dim background.
                ApplyToBackground(b => b.FadeColour(OsuColour.Gray(0.4f), PotatoMode.UltraActive ? 0 : 400, Easing.OutQuint));

                detachedPanel = expandedPanel;
            }
            else if (detachedPanel != null)
            {
                var screenSpacePos = detachedPanel.ScreenSpaceDrawQuad.TopLeft;

                // Remove from the local container and re-attach.
                detachedPanelContainer.Remove(detachedPanel, false);
                ScorePanelList.Attach(detachedPanel);

                // Move into its original location in the attached container first, then to the final location.
                float origLocation = detachedPanel.Parent!.ToLocalSpace(screenSpacePos).X;
                detachedPanel.MoveToX(origLocation)
                             .Then()
                             .MoveToX(0, PotatoMode.UltraActive ? 0 : 250, Easing.OutElasticQuarter);

                // Show contracted panels.
                foreach (var contracted in ScorePanelList.GetScorePanels().Where(p => p.State == PanelState.Contracted))
                    contracted.FadeIn(PotatoMode.UltraActive ? 0 : 150, Easing.OutQuint);
                ScorePanelList.HandleInput = true;

                // Un-dim background.
                ApplyToBackground(b => b.FadeColour(OsuColour.Gray(0.5f), PotatoMode.UltraActive ? 0 : 250, Easing.OutQuint));

                detachedPanel = null;
            }
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.QuickExit:
                    if (this.IsCurrentScreen())
                    {
                        this.Exit();
                        return true;
                    }

                    break;

                case GlobalAction.Select:
                    if (SelectedScore.Value != null)
                        StatisticsPanel.ToggleVisibility();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected partial class VerticalScrollContainer : OsuScrollContainer
        {
            protected override Container<Drawable> Content => content;

            private readonly Container content;

            protected override bool OnScroll(ScrollEvent e) => !e.ControlPressed && !e.AltPressed && !e.ShiftPressed && !e.SuperPressed;

            public VerticalScrollContainer()
            {
                Masking = false;

                base.Content.Add(content = new Container { RelativeSizeAxes = Axes.X });
            }

            protected override void Update()
            {
                base.Update();
                content.Height = Math.Max(screen_height, DrawHeight);
            }
        }
    }
}
