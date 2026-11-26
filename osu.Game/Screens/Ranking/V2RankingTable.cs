// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Select;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// TumThai: the full ranking of the beatmap the play was on (after the Rev 11 results design): Global, Country,
    /// Friends, Team or Local, optionally only with the play's mods, with the play pinned on top. Opened with the results
    /// footer's Ranking button on the modern layouts.
    /// </summary>
    public partial class V2RankingTable : VisibilityContainer
    {
        private const float row_height = 30;

        private readonly ScoreInfo score;

        private readonly Bindable<BeatmapLeaderboardScope> scope = new Bindable<BeatmapLeaderboardScope>(BeatmapLeaderboardScope.Global);
        private readonly BindableBool sameMods = new BindableBool();
        private readonly IBindable<LeaderboardScores?> scores = new Bindable<LeaderboardScores?>();

        private FillFlowContainer rows = null!;
        private Container pinned = null!;
        private OsuSpriteText status = null!;
        private List<HitResult> columns = new List<HitResult>();
        private LeaderboardCriteria? requested;

        // TumThai: its own manager, so switching tabs here never changes the board song select and gameplay share.
        private readonly LeaderboardManager leaderboardManager = new LeaderboardManager();

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public V2RankingTable(ScoreInfo score)
        {
            this.score = score;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            columns = score.GetStatisticsForDisplay().Where(s => s.Result.IsBasic()).Select(s => s.Result).ToList();

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Horizontal = 50, Top = 60, Bottom = 90 },
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    CornerRadius = 12,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4Extensions.FromHex(@"1c2328").Opacity(0.96f),
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Y = 16,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 8),
                            Children = new Drawable[]
                            {
                                new OsuTabControl<BeatmapLeaderboardScope>
                                {
                                    Width = 520,
                                    Height = 30,
                                    Current = scope,
                                },
                                new OsuCheckbox
                                {
                                    LabelText = @"Only with this play's mods",
                                    Width = 260,
                                    Current = sameMods,
                                },
                            },
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 100, Horizontal = 20, Bottom = 16 },
                            Children = new Drawable[]
                            {
                                pinned = new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = row_height + 8,
                                },
                                header(),
                                new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Top = row_height + 8 + 24 },
                                    Child = rows = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 2),
                                    },
                                },
                                status = new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold),
                                    Colour = Color4.White.Opacity(0.7f),
                                },
                            },
                        },
                    },
                },
            };

            pinned.Add(row(score, score.Position, highlighted: true));
            AddInternal(leaderboardManager);
            scores.BindTo(leaderboardManager.Scores);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            scope.BindValueChanged(_ => fetch());
            sameMods.BindValueChanged(_ => fetch());
            scores.BindValueChanged(_ => Schedule(show));
        }

        protected override void PopIn()
        {
            this.FadeIn(200, Easing.OutQuint);
            fetch();
        }

        protected override void PopOut() => this.FadeOut(200, Easing.OutQuint);

        // keep clicks from reaching the results underneath
        protected override bool OnClick(ClickEvent e) => true;

        private void fetch()
        {
            if (State.Value != Visibility.Visible)
                return;

            // the same board the results screen uses (a Relax/Autopilot play's own, by pp there), at the chosen scope
            bool byPp = LeaderboardVariantExtensions.VariantFor(score.Mods) != LeaderboardVariant.Vanilla
                        && (score.Ruleset.IsLegacyRuleset() || score.Ruleset.IsSpecialRuleset());

            requested = new LeaderboardCriteria(
                score.BeatmapInfo!,
                score.Ruleset.ForMods(score.Mods),
                scope.Value,
                sameMods.Value ? score.Mods : null,
                byPp ? LeaderboardSortMode.PerformancePoints : LeaderboardSortMode.Score,
                LeaderboardVariantExtensions.VariantFor(score.Mods));

            rows.Clear();
            status.Text = @"Loading...";
            leaderboardManager.FetchWithCriteria(requested, forceRefresh: true);
        }

        private void show()
        {
            if (requested == null || leaderboardManager.CurrentCriteria?.Equals(requested) != true)
                return;

            var result = scores.Value;
            rows.Clear();

            if (result == null)
                return;

            if (result.FailState != null)
            {
                status.Text = result.FailState.Value.GetLocalisableDescription().ToString();
                return;
            }

            var list = result.AllScores.ToList();
            status.Text = list.Count == 0 ? @"No scores here yet." : string.Empty;

            int i = 0;
            foreach (var s in list)
                rows.Add(row(s, s.Position ?? ++i, highlighted: s.UserID == score.UserID && s.MatchesOnlineID(score)));
        }

        private Drawable header()
        {
            var cells = new List<(float width, string text)>
            {
                (70, @"RANK"), (40, string.Empty), (110, @"SCORE"), (80, @"ACCURACY"), (200, @"PLAYER"), (90, @"MAX COMBO"),
            };
            cells.AddRange(columns.Select(r => (64f, r.GetDescription().ToUpperInvariant())));
            cells.AddRange(new[] { (70f, @"PP"), (90f, @"TIME"), (130f, @"MODS") });

            return new FillFlowContainer
            {
                Y = row_height + 10,
                RelativeSizeAxes = Axes.X,
                Height = 20,
                Direction = FillDirection.Horizontal,
                Padding = new MarginPadding { Horizontal = 12 },
                ChildrenEnumerable = cells.Select(c => new OsuSpriteText
                {
                    Width = c.width,
                    Text = c.text,
                    Font = OsuFont.Torus.With(size: 10, weight: FontWeight.Bold),
                    Colour = Color4.White.Opacity(0.6f),
                }),
            };
        }

        private Drawable row(ScoreInfo s, int? place, bool highlighted)
        {
            var stats = s.GetStatisticsForDisplay().ToDictionary(d => d.Result, d => d.Count);

            var cells = new List<Drawable>
            {
                text(70, place is int p ? $"#{p:N0}" : @"-", 14, FontWeight.Bold),
                new Container
                {
                    Width = 40,
                    RelativeSizeAxes = Axes.Y,
                    Child = new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(28, 14),
                        Child = new DrawableRank(s.Rank),
                    },
                },
                text(110, s.TotalScore.ToString(@"N0"), 14, FontWeight.SemiBold),
                text(80, s.DisplayAccuracy.ToString(), 14, FontWeight.SemiBold, s.Accuracy >= 1 ? colours.GreenLight : (Color4?)null),
                new FillFlowContainer
                {
                    Width = 200,
                    RelativeSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Children = new Drawable[]
                    {
                        new UpdateableAvatar(s.User, showUserPanelOnHover: true)
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(22),
                            CornerRadius = 11,
                            Masking = true,
                        },
                        new UpdateableFlag(s.User.CountryCode)
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(20, 14),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = s.User.Username,
                            Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold),
                        },
                    },
                },
                text(90, $"{s.MaxCombo:N0}x", 14, FontWeight.Regular),
            };

            cells.AddRange(columns.Select(r => text(64, (stats.TryGetValue(r, out int n) ? n : 0).ToString(@"N0"), 14, FontWeight.Regular,
                r == HitResult.Miss && stats.GetValueOrDefault(r) > 0 ? colours.Red : (Color4?)null)));

            cells.Add(text(70, s.PP is double pp ? $"{pp:N0}" : @"-", 14, FontWeight.SemiBold));
            cells.Add(new Container
            {
                Width = 90,
                RelativeSizeAxes = Axes.Y,
                Child = new DrawableDate(s.Date, 13)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
            });
            cells.Add(new Container
            {
                Width = 130,
                RelativeSizeAxes = Axes.Y,
                Child = new ModFlowDisplay
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Scale = new Vector2(0.4f),
                    Current = { Value = s.Mods },
                },
            });

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = row_height,
                CornerRadius = 6,
                Masking = true,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = highlighted ? Color4Extensions.FromHex(@"34424c") : Color4Extensions.FromHex(@"242e35"),
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Padding = new MarginPadding { Horizontal = 12 },
                        Children = cells,
                    },
                },
            };
        }

        private static Drawable text(float width, string value, float size, FontWeight weight, Color4? colour = null) => new Container
        {
            Width = width,
            RelativeSizeAxes = Axes.Y,
            Child = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = value,
                Font = OsuFont.Torus.With(size: size, weight: weight),
                Colour = colour ?? Color4.White,
            },
        };
    }
}
