// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Diagnostics;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Select;
using osuTK;
using APIUser = osu.Game.Online.API.Requests.Responses.APIUser;

namespace osu.Game.Overlays.BeatmapSet.Scores
{
    public partial class ScoresContainer : BeatmapSetLayoutSection
    {
        private const int spacing = 15;

        public readonly Bindable<APIBeatmap> Beatmap = new Bindable<APIBeatmap>();
        private readonly Bindable<IRulesetInfo> ruleset = new Bindable<IRulesetInfo>();
        private readonly Bindable<BeatmapLeaderboardScope> scope = new Bindable<BeatmapLeaderboardScope>(BeatmapLeaderboardScope.Global);
        private readonly IBindable<APIUser> user = new Bindable<APIUser>();

        // osu!somtum: the vanilla, Relax or Autopilot leaderboard.
        private readonly Bindable<LeaderboardVariant> variant = new Bindable<LeaderboardVariant>(LeaderboardVariant.Vanilla);
        private readonly LeaderboardVariantSelector variantSelector;

        // osu!somtum: by pp or by score. Defaults to pp on Relax/Autopilot, score on Vanilla.
        private readonly Bindable<LeaderboardSortMode> sort = new Bindable<LeaderboardSortMode>(LeaderboardSortMode.Score);
        private readonly LeaderboardSortSelector sortSelector;

        // Relax and Autopilot boards, and boards sorted by pp, come ordered by the server; keep that order.
        private bool keepServerOrder;

        // osu!somtum: whether the last request asked for the board by pp.
        private bool requestedByPerformance;

        private readonly Box background;
        private readonly ScoreTable scoreTable;
        private readonly FillFlowContainer topScoresContainer;
        private readonly LoadingLayer loading;
        private readonly LeaderboardModSelector modSelector;
        private readonly NoScoresPlaceholder noScoresPlaceholder;
        private readonly NotSupporterPlaceholder notSupporterPlaceholder;
        private readonly NoTeamPlaceholder noTeamPlaceholder;

        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; }

        private GetScoresRequest getScoresRequest;

        private CancellationTokenSource loadCancellationSource;

        protected APIScoresCollection Scores
        {
            set => Schedule(() =>
            {
                loadCancellationSource?.Cancel();
                loadCancellationSource = new CancellationTokenSource();

                topScoresContainer.Clear();
                scoreTable.ClearScores();
                scoreTable.Hide();

                loading.Hide();
                loading.FinishTransforms();

                if (value?.Scores.Any() != true)
                    return;

                var apiBeatmap = Beatmap.Value;

                Debug.Assert(apiBeatmap != null);

                // TODO: temporary. should be removed once `OrderByTotalScore` can accept `IScoreInfo`.
                var beatmapInfo = new BeatmapInfo
                {
#pragma warning disable 618
                    MaxCombo = apiBeatmap.MaxCombo,
#pragma warning restore 618
                    Status = apiBeatmap.Status,
                    MD5Hash = apiBeatmap.MD5Hash
                };

                var scores = value.Scores.Select(s => s.ToScoreInfo(rulesets, beatmapInfo)).ToArray();

                if (!keepServerOrder)
                    scores = scores.OrderByTotalScore().ToArray();

                var topScore = scores.First();

                bool showPerformance = apiBeatmap.Status.GrantsPerformancePoints();

                // osu!somtum: show pp in place of score when sorted by pp.
                bool sortedByPerformance = showPerformance && requestedByPerformance;

                scoreTable.DisplayScores(scores, showPerformance, sortedByPerformance);
                scoreTable.Show();

                var userScore = value.UserScore;
                var userScoreInfo = userScore?.Score.ToScoreInfo(rulesets, beatmapInfo);

                topScoresContainer.Add(new DrawableTopScore(topScore, sortedByPerformance: sortedByPerformance));

                if (userScoreInfo != null && userScoreInfo.OnlineID != topScore.OnlineID)
                    topScoresContainer.Add(new DrawableTopScore(userScoreInfo, userScore.Position, sortedByPerformance));
            });
        }

        public ScoresContainer()
        {
            AddRange(new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING },
                    Margin = new MarginPadding { Vertical = 20 },
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, spacing),
                            Children = new Drawable[]
                            {
                                variantSelector = new LeaderboardVariantSelector
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Alpha = 0,
                                    Current = { BindTarget = variant }
                                },
                                sortSelector = new LeaderboardSortSelector
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Alpha = 0,
                                    Current = { BindTarget = sort }
                                },
                                new LeaderboardScopeSelector
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Current = { BindTarget = scope }
                                },
                                modSelector = new LeaderboardModSelector
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Ruleset = { BindTarget = ruleset }
                                }
                            }
                        },
                        new Container
                        {
                            AutoSizeAxes = Axes.Y,
                            RelativeSizeAxes = Axes.X,
                            Margin = new MarginPadding { Top = spacing },
                            Children = new Drawable[]
                            {
                                noScoresPlaceholder = new NoScoresPlaceholder
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Alpha = 0,
                                    AlwaysPresent = true,
                                    Margin = new MarginPadding { Vertical = 10 }
                                },
                                noTeamPlaceholder = new NoTeamPlaceholder
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Margin = new MarginPadding { Vertical = 10 },
                                    Alpha = 0,
                                },
                                notSupporterPlaceholder = new NotSupporterPlaceholder
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Margin = new MarginPadding { Vertical = 10 },
                                    Alpha = 0,
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, spacing),
                                    Children = new Drawable[]
                                    {
                                        topScoresContainer = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 5),
                                        },
                                        scoreTable = new ScoreTable
                                        {
                                            Anchor = Anchor.TopCentre,
                                            Origin = Anchor.TopCentre,
                                        }
                                    }
                                },
                            }
                        }
                    },
                },
                loading = new LoadingLayer()
            });
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            background.Colour = colourProvider.Background5;

            user.BindTo(api.LocalUser);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            scope.BindValueChanged(_ => getScores());
            ruleset.BindValueChanged(_ => getScores());
            variant.BindValueChanged(_ =>
            {
                // osu!somtum: a new variant starts on its default sort.
                if (!resetSort())
                    getScores();
            });
            sort.BindValueChanged(_ => getScores());

            modSelector.SelectedMods.CollectionChanged += (_, _) => getScores();

            Beatmap.BindValueChanged(onBeatmapChanged);
            user.BindValueChanged(onUserChanged, true);
        }

        private void onBeatmapChanged(ValueChangedEvent<APIBeatmap> beatmap)
        {
            var beatmapRuleset = beatmap.NewValue?.Ruleset;

            updateVariants(beatmapRuleset);
            resetSort();
            sortSelector.Alpha = beatmap.NewValue?.Status.GrantsPerformancePoints() == true ? 1 : 0;

            if (ruleset.Value?.OnlineID == beatmapRuleset?.OnlineID)
            {
                modSelector.DeselectAll();
                ruleset.TriggerChange();
            }
            else
                ruleset.Value = beatmapRuleset;

            scope.Value = BeatmapLeaderboardScope.Global;
        }

        private void updateVariants(IRulesetInfo beatmapRuleset)
        {
            var supported = beatmapRuleset.SupportedVariants();

            if (!supported.Contains(variant.Value))
                variant.Value = LeaderboardVariant.Vanilla;

            variantSelector.Items = supported;
            variantSelector.Alpha = supported.Length > 1 ? 1 : 0;
        }

        /// <summary>
        /// osu!somtum: sets the sort to the current variant's default.
        /// </summary>
        /// <returns>Whether the sort changed (which refetches the scores).</returns>
        private bool resetSort()
        {
            var defaultSort = variant.Value == LeaderboardVariant.Vanilla ? LeaderboardSortMode.Score : LeaderboardSortMode.PerformancePoints;

            if (sort.Value == defaultSort)
                return false;

            sort.Value = defaultSort;
            return true;
        }

        private void onUserChanged(ValueChangedEvent<APIUser> user)
        {
            if (modSelector.SelectedMods.Any())
                modSelector.DeselectAll();
            else
                getScores();
        }

        private void getScores()
        {
            getScoresRequest?.Cancel();
            getScoresRequest = null;

            noScoresPlaceholder.Hide();
            noTeamPlaceholder.Hide();
            notSupporterPlaceholder.Hide();

            if (Beatmap.Value == null || Beatmap.Value.OnlineID <= 0 || (Beatmap.Value.Status <= BeatmapOnlineStatus.Pending))
            {
                Scores = null;
                Hide();
                return;
            }

            if ((scope.Value == BeatmapLeaderboardScope.Team) && user.Value.Team == null)
            {
                Scores = null;
                noTeamPlaceholder.Show();
                return;
            }

            if (scope.Value.RequiresSupporter(modSelector.SelectedMods.Count > 0) && !userIsSupporter)
            {
                Scores = null;
                notSupporterPlaceholder.Show();
                return;
            }

            Show();
            loading.Show();

            IRulesetInfo requestRuleset = Beatmap.Value.Ruleset;

            if (variant.Value != LeaderboardVariant.Vanilla && rulesets.GetRuleset(requestRuleset.OnlineID) is RulesetInfo baseRuleset)
                requestRuleset = baseRuleset.ApplyVariant(variant.Value);

            // osu!somtum: only ask for a sort on maps that give pp; otherwise the server's own order is used.
            LeaderboardSortMode? requestSort = Beatmap.Value.Status.GrantsPerformancePoints() ? sort.Value : null;

            requestedByPerformance = requestSort == LeaderboardSortMode.PerformancePoints;
            keepServerOrder = requestedByPerformance || requestRuleset.IsSpecialRuleset();
            getScoresRequest = new GetScoresRequest(Beatmap.Value, requestRuleset, scope.Value, modSelector.SelectedMods, requestSort);
            getScoresRequest.Success += scores =>
            {
                Scores = scores;

                if (!scores.Scores.Any())
                    noScoresPlaceholder.ShowWithScope(scope.Value);
            };

            api.Queue(getScoresRequest);
        }

        private bool userIsSupporter => api.IsLoggedIn && api.LocalUser.Value.IsSupporter;
    }
}
