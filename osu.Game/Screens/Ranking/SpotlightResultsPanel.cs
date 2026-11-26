// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// TumThai: the "Spotlight" results layout: the beatmap's leaderboard down the left with this play's place, the
    /// accuracy ring around the beatmap's cover in the middle, and the grade, score and numbers on the right.
    /// Laid out on a 1149x655 design and scaled to the screen.
    /// </summary>
    public partial class SpotlightResultsPanel : CompositeDrawable
    {
        private const float design_width = 1149;
        private const float design_height = 655;
        private const float right_panel_x = 637;

        private readonly ScoreInfo score;
        private readonly bool withFlair;

        private FillFlowContainer leaderboard = null!;
        private int shownLeaderboardCount = -1;

        [Resolved]
        private ResultsScreen? resultsScreen { get; set; }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public SpotlightResultsPanel(ScoreInfo score, bool withFlair = false)
        {
            this.score = score;
            this.withFlair = withFlair;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(BeatmapManager beatmaps, BeatmapDifficultyCache difficultyCache, AudioManager audio)
        {
            tickSample = audio.Samples.Get(@"Results/score-tick-lesser");

            var working = beatmaps.GetWorkingBeatmap(score.BeatmapInfo);

            InternalChild = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = new Vector2(design_width, design_height),
                Strategy = DrawSizePreservationStrategy.Minimum,
                Child = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(design_width, design_height),
                    Children = new Drawable[]
                    {
                        // left: darkened backdrop for the leaderboard and ring
                        new Box
                        {
                            Size = new Vector2(right_panel_x, design_height),
                            Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.45f), Color4.Black.Opacity(0.15f)),
                        },
                        // right: the numbers panel
                        new Box
                        {
                            X = right_panel_x,
                            Size = new Vector2(design_width - right_panel_x, design_height),
                            Colour = Color4Extensions.FromHex(@"2b3a45").Opacity(0.95f),
                        },
                        new Box
                        {
                            X = right_panel_x,
                            Y = 388,
                            Size = new Vector2(design_width - right_panel_x, 155),
                            Colour = Color4Extensions.FromHex(@"23303a"),
                        },
                        new Box
                        {
                            X = right_panel_x,
                            Y = 543,
                            Size = new Vector2(design_width - right_panel_x, design_height - 543),
                            Colour = Color4Extensions.FromHex(@"1c262e"),
                        },
                        leaderboard = new FillFlowContainer
                        {
                            X = 24,
                            Width = 150,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 4),
                        },
                        // middle: the cover inside the accuracy ring
                        new Container
                        {
                            Position = new Vector2(427, 322),
                            Origin = Anchor.Centre,
                            Size = new Vector2(520),
                            Children = new Drawable[]
                            {
                                new AccuracyCircle(score, withFlair)
                                {
                                    UseV2Style = true,
                                    ShowRankInCentre = false,
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new CircularContainer
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    RelativeSizeAxes = Axes.Both,
                                    Size = new Vector2(0.86f),
                                    Masking = true,
                                    BorderThickness = 5,
                                    BorderColour = Color4.Black,
                                    Children = new Drawable[]
                                    {
                                        new BeatmapBackgroundSprite(working)
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            RelativeSizeAxes = Axes.Both,
                                            FillMode = FillMode.Fill,
                                        },
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0), Color4.Black.Opacity(0.75f)),
                                        },
                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.BottomCentre,
                                            Origin = Anchor.BottomCentre,
                                            Y = -48,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 2),
                                            Children = new Drawable[]
                                            {
                                                centred(new TruncatingSpriteText
                                                {
                                                    Text = new RomanisableString(score.BeatmapInfo!.Metadata.TitleUnicode, score.BeatmapInfo.Metadata.Title),
                                                    Font = OsuFont.Torus.With(size: 17, weight: FontWeight.SemiBold),
                                                    MaxWidth = 300,
                                                }),
                                                centred(new TruncatingSpriteText
                                                {
                                                    Text = new RomanisableString(score.BeatmapInfo.Metadata.ArtistUnicode, score.BeatmapInfo.Metadata.Artist),
                                                    Font = OsuFont.Torus.With(size: 12, weight: FontWeight.SemiBold),
                                                    Colour = Color4Extensions.FromHex(@"ffd98f"),
                                                    MaxWidth = 300,
                                                }),
                                                centred(new TruncatingSpriteText
                                                {
                                                    Margin = new MarginPadding { Top = 8 },
                                                    Text = $"[{score.BeatmapInfo.DifficultyName}] mapped by {score.BeatmapInfo.Metadata.Author.Username}",
                                                    Font = OsuFont.Torus.With(size: 10, weight: FontWeight.SemiBold),
                                                    MaxWidth = 300,
                                                }),
                                            },
                                        },
                                    },
                                },
                                // the star rating on a curved band along the top of the cover, in its difficulty colour
                                starArc = new CircularProgress
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    RelativeSizeAxes = Axes.Both,
                                    Size = new Vector2(0.79f),
                                    InnerRadius = 0.11f,
                                    RoundedCaps = true,
                                    Progress = 0.16,
                                    Rotation = -0.08f * 360,
                                },
                                starText = new OsuSpriteText
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Y = 57,
                                    Font = OsuFont.Torus.With(size: 15, weight: FontWeight.Bold),
                                    Colour = Color4Extensions.FromHex(@"2a1820"),
                                },
                            },
                        },
                        // right: grade, score, mods, numbers, player
                        new V2RankEmblem(score.Rank)
                        {
                            AutoAppear = true,
                            Position = new Vector2(776, 195),
                            Origin = Anchor.Centre,
                            Scale = new Vector2(0.5f),
                        },
                        scoreText = new OsuSpriteText
                        {
                            Position = new Vector2(712, 268),
                            Text = (withFlair ? 0 : score.TotalScore).ToString(@"N0"),
                            Font = OsuFont.Torus.With(size: 72, weight: FontWeight.Light, fixedWidth: true),
                            Spacing = new Vector2(-4, 0),
                            Colour = ColourInfo.GradientVertical(Color4.White, Color4Extensions.FromHex(@"d8f3ff")),
                        },
                        mods = new ModFlowDisplay
                        {
                            Position = new Vector2(714, 345),
                            AutoSizeAxes = Axes.Both,
                            Scale = new Vector2(0.55f),
                            Current = { Value = score.Mods },
                        },
                        tiles = new FillFlowContainer
                        {
                            Position = new Vector2(715, 393),
                            Width = 370,
                            AutoSizeAxes = Axes.Y,
                            Spacing = new Vector2(4),
                            ChildrenEnumerable = mainTiles().Concat(judgementTiles()),
                        },
                        playerAvatar = new UpdateableAvatar(score.User, showUserPanelOnHover: true)
                        {
                            Position = new Vector2(717, 512),
                            Size = new Vector2(24),
                            CornerRadius = 5,
                            Masking = true,
                        },
                        playerName = new OsuSpriteText
                        {
                            Position = new Vector2(747, 515),
                            Text = score.User.Username,
                            Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold),
                        },
                        new OsuSpriteText
                        {
                            Position = new Vector2(900, 510),
                            Text = @"Achieved on",
                            Font = OsuFont.Torus.With(size: 9, weight: FontWeight.SemiBold),
                            Colour = Color4.White.Opacity(0.7f),
                        },
                        new OsuSpriteText
                        {
                            Position = new Vector2(900, 522),
                            Text = score.Date.ToLocalTime().ToString(@"d MMMM yyyy, h:mmtt"),
                            Font = OsuFont.Torus.With(size: 11, weight: FontWeight.SemiBold),
                        },
                    },
                },
            };

            showStars(score.BeatmapInfo.StarRating);

            difficultyCache.GetDifficultyAsync(score.BeatmapInfo, score.Ruleset, score.Mods)
                           .ContinueWith(t => Schedule(() =>
                           {
                               if (t.IsCompletedSuccessfully && t.GetResultSafely() is StarDifficulty d)
                                   showStars(d.Stars);
                           }));
        }

        private OsuSpriteText scoreText = null!;
        private Drawable mods = null!;
        private FillFlowContainer tiles = null!;
        private Drawable playerAvatar = null!;
        private Drawable playerName = null!;
        private Sample? tickSample;
        private double lastTick;

        private long displayedScore;

        /// <summary>The score as shown while it counts up (with a tick now and then, rising as it goes).</summary>
        public long DisplayedScore
        {
            get => displayedScore;
            set
            {
                displayedScore = value;
                scoreText.Text = value.ToString(@"N0");

                if (withFlair && Time.Current - lastTick > 60 && value < score.TotalScore)
                {
                    lastTick = Time.Current;
                    var channel = tickSample?.GetChannel();

                    if (channel != null)
                    {
                        channel.Frequency.Value = 1 + 0.5 * value / Math.Max(1, score.TotalScore);
                        channel.Play();
                    }
                }
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!withFlair)
                return;

            // The score counts up while the ring fills (same timing as the ring).
            using (BeginDelayedSequence(AccuracyCircle.ACCURACY_TRANSFORM_DELAY))
                this.TransformTo(nameof(DisplayedScore), score.TotalScore, AccuracyCircle.ACCURACY_TRANSFORM_DURATION, AccuracyCircle.ACCURACY_TRANSFORM_EASING);

            // The right side comes in piece by piece, the leaderboard from the left.
            double at = 300;

            foreach (var d in new[] { mods }.Concat(tiles.Children).Append(playerAvatar).Append(playerName))
            {
                float x = d.X;
                d.FadeOut().MoveToX(x + 24).Delay(at).FadeIn(300, Easing.OutQuint).MoveToX(x, 500, Easing.OutQuint);
                at += 70;
            }

            leaderboard.FadeOut().MoveToX(-160).Delay(200).FadeIn(400, Easing.OutQuint).MoveToX(24, 700, Easing.OutQuint);
        }

        private void showStars(double stars)
        {
            starArc.Colour = colours.ForStarDifficulty(stars);
            starText.Colour = colours.ForStarDifficultyText(stars);
            starText.Text = $"★ {stars:0.00}";
        }

        private CircularProgress starArc = null!;
        private OsuSpriteText starText = null!;

        private static Drawable centred(Drawable d)
        {
            d.Anchor = Anchor.TopCentre;
            d.Origin = Anchor.TopCentre;
            return d;
        }

        private IEnumerable<Drawable> mainTiles()
        {
            yield return tile(122, @"ACCURACY", score.DisplayAccuracy.ToString(), 20, null, score.Accuracy);
            int maxCombo = score.GetMaximumAchievableCombo();
            yield return tile(122, @"MAX COMBO", $"{score.MaxCombo}x", 20, null, maxCombo > 0 ? (double)score.MaxCombo / maxCombo : null);
            yield return tile(118, @"PP", score.PP is double pp ? $"{pp:N0}" : @"-", 20, null);
        }

        private IEnumerable<Drawable> judgementTiles()
        {
            var shown = score.GetStatisticsForDisplay().Where(s => s.Result.IsBasic()).ToList();
            float width = (370 - 4 * (shown.Count - 1)) / Math.Max(1, shown.Count);

            foreach (var s in shown)
                yield return tile(width, s.DisplayName.ToString().ToUpperInvariant(), s.Count.ToString(), 14, colours.ForHitResult(s.Result));
        }

        private static Drawable tile(float width, string header, string value, float valueSize, Color4? accent, double? progress = null) => new Container
        {
            Size = new Vector2(width, valueSize > 16 ? 42 : 33),
            CornerRadius = 5,
            Masking = true,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4Extensions.FromHex(@"111a20").Opacity(0.85f),
                },
                new OsuSpriteText
                {
                    Position = new Vector2(7, 4),
                    Text = header,
                    Font = OsuFont.Torus.With(size: 8, weight: FontWeight.Bold),
                },
                progress is double fraction
                    ? new Container
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -9,
                        Size = new Vector2(15),
                        Children = new Drawable[]
                        {
                            new CircularProgress { RelativeSizeAxes = Axes.Both, InnerRadius = 0.22f, Progress = 1, Colour = Color4.White.Opacity(0.15f) },
                            new CircularProgress { RelativeSizeAxes = Axes.Both, InnerRadius = 0.22f, RoundedCaps = true, Progress = Math.Clamp(fraction, 0, 1), Colour = Color4Extensions.FromHex(@"88ffcc") },
                        },
                    }
                    : Empty(),
                accent is Color4 a
                    ? new Box
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Position = new Vector2(-7, 7),
                        Size = new Vector2(12, 2.5f),
                        Colour = a,
                    }
                    : Empty(),
                new OsuSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Position = new Vector2(7, -3),
                    Text = value,
                    Font = OsuFont.Torus.With(size: valueSize, weight: FontWeight.Regular),
                },
            },
        };

        protected override void Update()
        {
            base.Update();

            // The leaderboard sits so this play's tag is level with the ring's centre.
            if (selfRow?.IsLoaded == true && selfRow.Parent != null)
                leaderboard.Y = 322 - (selfRow.Y + selfRow.DrawHeight / 2);

            // Clear of the results footer (laid out for a 1280-wide screen, scaled with the width).
            Padding = new MarginPadding { Bottom = DrawWidth * V2ResultsFooter.DESIGN_HEIGHT / 1280 };

            // The leaderboard arrives after the screen does: rebuild when it changes.
            var scores = resultsScreen?.GetOverviewScores().ToList() ?? new List<ScoreInfo> { score };
            if (scores.Count != shownLeaderboardCount)
            {
                shownLeaderboardCount = scores.Count;
                rebuildLeaderboard(scores);
            }
        }

        private void rebuildLeaderboard(List<ScoreInfo> scores)
        {
            var ordered = scores.OrderByDescending(s => s.TotalScore).ToList();
            int self = Math.Max(0, ordered.FindIndex(s => ReferenceEquals(s, score) || s.Equals(score)));

            // Up to 8 above and 8 below this play.
            int first = Math.Max(0, self - 8);
            leaderboard.Clear();

            for (int i = first; i < Math.Min(ordered.Count, self + 9); i++)
            {
                var s = ordered[i];
                // a play without a place of its own (not on the server's board yet) comes right after the one above it
                int place = s.Position ?? (i > 0 && ordered[i - 1].Position is int above ? above + 1 : i + 1);
                leaderboard.Add(i == self ? selfRow = createSelfRow(place) : row(s, place));
            }
        }

        private static Drawable row(ScoreInfo s, int place) => new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = 22,
            Children = new Drawable[]
            {
                new UpdateableAvatar(s.User, isInteractive: false)
                {
                    Size = new Vector2(20),
                    CornerRadius = 10,
                    Masking = true,
                },
                new OsuSpriteText
                {
                    X = 26,
                    Text = s.User.Username,
                    Font = OsuFont.Torus.With(size: 9, weight: FontWeight.SemiBold),
                    Colour = Color4.White.Opacity(0.8f),
                },
                new OsuSpriteText
                {
                    X = 26,
                    Y = 9,
                    Text = ordinal(place),
                    Font = OsuFont.Torus.With(size: 11, weight: FontWeight.SemiBold),
                },
            },
        };

        private Drawable? selfRow;

        private static Drawable createSelfRow(int place) => new Container
        {
            Width = 150,
            Height = 38,
            Margin = new MarginPadding { Vertical = 6 },
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Width = 0.85f,
                    Colour = ColourInfo.GradientHorizontal(Color4Extensions.FromHex(@"a8f7c7"), Color4Extensions.FromHex(@"7ef1ff")),
                },
                // the arrow end, pointing at the ring
                new Triangle
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.Centre,
                    X = -10,
                    Size = new Vector2(38, 20),
                    Rotation = 90,
                    Colour = Color4Extensions.FromHex(@"7ef1ff"),
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 14,
                    Text = ordinal(place),
                    Font = OsuFont.Torus.With(size: 22, weight: FontWeight.Regular),
                    Colour = Color4Extensions.FromHex(@"1c262e"),
                },
            },
        };

        private static string ordinal(int n)
        {
            string suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return $"{n:N0}{suffix}";
        }
    }
}
