// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Ranking.Expanded;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// TumThai: the results screen as drawn in ppy's "Result Screen Rev 11" mockups (Main Score Screen): the beatmap header,
    /// the big score, slanted tiles (accuracy, combo, pp; the judgements in their colours; the rest), mods with their
    /// multiplier, the player, and the accuracy ring with the grade. Laid out on the mockup's 1920x1080 and scaled.
    /// </summary>
    public partial class Rev11ResultsPanel : CompositeDrawable
    {
        private const float shear = 0.2f;

        private readonly ScoreInfo score;
        private readonly bool withFlair;

        private BigScoreCounter scoreCounter = null!;
        private readonly List<Drawable> appearing = new List<Drawable>();

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public Rev11ResultsPanel(ScoreInfo score, bool withFlair = false)
        {
            this.score = score;
            this.withFlair = withFlair;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var beatmap = score.BeatmapInfo!;
            var stats = score.GetStatisticsForDisplay().ToList();
            var basic = stats.Where(s => s.Result.IsBasic()).ToList();
            var other = stats.Where(s => !s.Result.IsBasic()).Take(3).ToList();

            double multiplier = 1;

            try
            {
                multiplier = score.Ruleset.CreateInstance().CreateScoreMultiplierCalculator(new ScoreMultiplierContext(beatmap.Difficulty, score)).CalculateFor(score.Mods);
            }
            catch
            {
                // a ruleset without one: show 1x
            }

            InternalChild = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = new Vector2(1920, 1080),
                Strategy = DrawSizePreservationStrategy.Minimum,
                Child = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(1920, 1080),
                    Children = new Drawable[]
                    {
                        // header: badges, title, artist; the difficulty line under it
                        appear(new Container
                        {
                            Position = new Vector2(0, 183),
                            Size = new Vector2(620, 74),
                            Children = new Drawable[]
                            {
                                band(620, 74, 0.55f),
                                new FillFlowContainer
                                {
                                    Position = new Vector2(72, 10),
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        new BeatmapSetOnlineStatusPill
                                        {
                                            Status = beatmap.Status,
                                            TextSize = 10,
                                            TextPadding = new MarginPadding { Horizontal = 6, Vertical = 1 },
                                        },
                                        new TruncatingSpriteText
                                        {
                                            Text = new RomanisableString(beatmap.Metadata.TitleUnicode, beatmap.Metadata.Title),
                                            Font = OsuFont.Torus.With(size: 24, weight: FontWeight.SemiBold),
                                            MaxWidth = 520,
                                        },
                                        new TruncatingSpriteText
                                        {
                                            Text = new RomanisableString(beatmap.Metadata.ArtistUnicode, beatmap.Metadata.Artist),
                                            Font = OsuFont.Torus.With(size: 15, weight: FontWeight.SemiBold),
                                            MaxWidth = 520,
                                        },
                                    },
                                },
                            },
                        }),
                        appear(new Container
                        {
                            Position = new Vector2(0, 258),
                            Size = new Vector2(610, 38),
                            Children = new Drawable[]
                            {
                                band(610, 38, 0.75f),
                                new FillFlowContainer
                                {
                                    Position = new Vector2(72, 0),
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(10, 0),
                                    Children = new Drawable[]
                                    {
                                        new StarRatingDisplay(new StarDifficulty(beatmap.StarRating, 0), StarRatingDisplaySize.Small)
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        },
                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Children = new Drawable[]
                                            {
                                                new OsuSpriteText
                                                {
                                                    Text = beatmap.DifficultyName,
                                                    Font = OsuFont.Torus.With(size: 15, weight: FontWeight.Bold),
                                                    Colour = colours.ForStarDifficulty(beatmap.StarRating),
                                                },
                                                new OsuSpriteText
                                                {
                                                    Text = @" mapped by ",
                                                    Font = OsuFont.Torus.With(size: 13, weight: FontWeight.SemiBold),
                                                    Colour = colours.ForStarDifficulty(beatmap.StarRating).Opacity(0.8f),
                                                },
                                                new OsuSpriteText
                                                {
                                                    Text = beatmap.Metadata.Author.Username,
                                                    Font = OsuFont.Torus.With(size: 13, weight: FontWeight.Bold),
                                                    Colour = colours.ForStarDifficulty(beatmap.StarRating),
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        }),
                        // the score
                        appear(new Container
                        {
                            Position = new Vector2(0, 305),
                            Size = new Vector2(840, 180),
                            Children = new Drawable[]
                            {
                                band(840, 180, 0.6f),
                                scoreCounter = new BigScoreCounter(withFlair)
                                {
                                    Position = new Vector2(66, 8),
                                    RelativeSizeAxes = Axes.None,
                                    AutoSizeAxes = Axes.Both,
                                    Current = { Value = withFlair ? 0 : score.TotalScore },
                                },
                                new V2ScoreFlair
                                {
                                    Position = new Vector2(720, 85),
                                    Origin = Anchor.Centre,
                                    Scale = new Vector2(1.6f),
                                    Alpha = score.IsPerfectCombo() ? 1 : 0,
                                },
                            },
                        }),
                        // accuracy / combo / pp
                        appear(tile(new Vector2(130, 492), new Vector2(236, 78), @"ACCURACY", score.DisplayAccuracy.ToString(), null)),
                        appear(tile(new Vector2(372, 492), new Vector2(236, 78), @"COMBO", $"{score.MaxCombo}x", null, $"/{score.GetMaximumAchievableCombo()}")),
                        appear(tile(new Vector2(614, 492), new Vector2(236, 78), @"PP", score.PP is double pp ? $"{pp:N0}" : @"-", null)),
                        // the judgements in their colours
                        appear(new FillFlowContainer
                        {
                            Position = new Vector2(116, 577),
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(6, 0),
                            ChildrenEnumerable = basic.Select((s, i) => judgementTile(s, i == 0 ? 236f : 165f)),
                        }),
                        // the rest (slider ends, spinner bonus...)
                        appear(new FillFlowContainer
                        {
                            Position = new Vector2(100, 697),
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(6, 0),
                            ChildrenEnumerable = other.Select(s => tile(Vector2.Zero, new Vector2(154, 70),
                                s.DisplayName.ToString().ToUpperInvariant(), s.Count.ToString(@"N0"), null, s.MaxCount is int max ? $"/{max}" : null, valueSize: 26)),
                        }),
                        // mods and multiplier
                        appear(new Container
                        {
                            Position = new Vector2(90, 772),
                            Size = new Vector2(470, 46),
                            Children = new Drawable[]
                            {
                                shearedBox(new Vector2(470, 46), Color4Extensions.FromHex(@"20262b").Opacity(0.92f)),
                                label(new Vector2(16, 0), @"MODS", Anchor.CentreLeft),
                                new ModFlowDisplay
                                {
                                    Position = new Vector2(76, 0),
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    AutoSizeAxes = Axes.Both,
                                    Scale = new Vector2(0.5f),
                                    Current = { Value = score.Mods },
                                },
                                label(new Vector2(-82, 0), @"MULTIPLIER", Anchor.CentreRight),
                                new Container
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    X = -16,
                                    AutoSizeAxes = Axes.Both,
                                    CornerRadius = 9,
                                    Masking = true,
                                    Children = new Drawable[]
                                    {
                                        new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex(@"6b1e2a") },
                                        new OsuSpriteText
                                        {
                                            Margin = new MarginPadding { Horizontal = 8, Vertical = 1 },
                                            Text = $"{multiplier:0.##}x",
                                            Font = OsuFont.Torus.With(size: 14, weight: FontWeight.Bold),
                                        },
                                    },
                                },
                            },
                        }),
                        // the player
                        appear(new Container
                        {
                            Position = new Vector2(0, 842),
                            Size = new Vector2(550, 58),
                            Children = new Drawable[]
                            {
                                band(550, 58, 0.75f),
                                new OsuSpriteText
                                {
                                    Position = new Vector2(84, 0),
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = score.Position is int place ? $"#{place:N0}" : @"-",
                                    Font = OsuFont.Torus.With(size: 22, weight: FontWeight.Regular),
                                },
                                new UpdateableAvatar(score.User, showUserPanelOnHover: true)
                                {
                                    Position = new Vector2(154, 0),
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(62, 58),
                                    CornerRadius = 6,
                                    Masking = true,
                                },
                                new OsuSpriteText
                                {
                                    Position = new Vector2(228, 6),
                                    Text = score.User.Username,
                                    Font = OsuFont.Torus.With(size: 20, weight: FontWeight.SemiBold),
                                },
                                new OsuSpriteText
                                {
                                    Position = new Vector2(228, 32),
                                    Text = $"Achieved on {score.Date.ToLocalTime():yyyy/MM/dd HH:mm}",
                                    Font = OsuFont.Torus.With(size: 11, weight: FontWeight.SemiBold),
                                    Colour = Color4.White.Opacity(0.85f),
                                },
                            },
                        }),
                        // the ring and the grade
                        new Container
                        {
                            Position = new Vector2(1520, 540),
                            Origin = Anchor.Centre,
                            Size = new Vector2(700),
                            Child = new AccuracyCircle(score, withFlair)
                            {
                                UseV2Style = true,
                                RelativeSizeAxes = Axes.Both,
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (withFlair)
            {
                using (BeginDelayedSequence(AccuracyCircle.ACCURACY_TRANSFORM_DELAY))
                    scoreCounter.Current.Value = score.TotalScore;

                double at = 150;

                foreach (var d in appearing)
                {
                    float x = d.X;
                    d.FadeOut().MoveToX(x - 40).Delay(at).FadeIn(300, Easing.OutQuint).MoveToX(x, 600, Easing.OutQuint);
                    at += 60;
                }
            }
        }

        private Drawable appear(Drawable d)
        {
            appearing.Add(d);
            return d;
        }

        /// <summary>A dark band reaching in from the left edge, its right end slanted.</summary>
        private static Drawable band(float width, float height, float alpha) => new Container
        {
            Size = new Vector2(width + 60, height),
            X = -60,
            Shear = new Vector2(shear, 0),
            Masking = true,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4Extensions.FromHex(@"1c2125").Opacity(alpha),
            },
        };

        private static Drawable shearedBox(Vector2 size, ColourInfo colour) => new Container
        {
            Size = size,
            Shear = new Vector2(shear, 0),
            CornerRadius = 5,
            Masking = true,
            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
        };

        private static Drawable label(Vector2 position, string text, Anchor anchor) => new OsuSpriteText
        {
            Anchor = anchor,
            Origin = anchor,
            Position = position,
            Text = text,
            Font = OsuFont.Torus.With(size: 14, weight: FontWeight.Bold),
        };

        private Drawable tile(Vector2 position, Vector2 size, string header, string value, ColourInfo? valueColour, string? suffix = null, float valueSize = 36) => new Container
        {
            Position = position,
            Size = size,
            Children = new Drawable[]
            {
                shearedBox(size, Color4Extensions.FromHex(@"20262b").Opacity(0.92f)),
                new OsuSpriteText
                {
                    Position = new Vector2(20, 10),
                    Text = header,
                    Font = OsuFont.Torus.With(size: 14, weight: FontWeight.Bold),
                },
                new FillFlowContainer
                {
                    Position = new Vector2(18, -6),
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Text = value,
                            Font = OsuFont.Torus.With(size: valueSize, weight: FontWeight.Regular),
                            Colour = valueColour ?? Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Margin = new MarginPadding { Left = 4, Bottom = 4 },
                            Text = suffix ?? string.Empty,
                            Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold),
                            Colour = Color4.White.Opacity(0.6f),
                        },
                    },
                },
            },
        };

        private Drawable judgementTile(HitResultDisplayStatistic s, float width)
        {
            Color4 c = colours.ForHitResult(s.Result);
            var size = new Vector2(width, 78);

            return new Container
            {
                Size = size,
                Children = new Drawable[]
                {
                    // the judgement's colour, fading into the dark tile, with a bright edge
                    new Container
                    {
                        Size = size,
                        Shear = new Vector2(shear, 0),
                        CornerRadius = 5,
                        Masking = true,
                        BorderThickness = 2.5f,
                        BorderColour = c,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex(@"20262b").Opacity(0.92f) },
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = ColourInfo.GradientVertical(c.Opacity(0.45f), c.Opacity(0.15f)),
                            },
                        },
                    },
                    new OsuSpriteText
                    {
                        Position = new Vector2(20, 10),
                        Text = s.DisplayName.ToString().ToUpperInvariant(),
                        Font = OsuFont.Torus.With(size: 14, weight: FontWeight.Bold),
                    },
                    new OsuSpriteText
                    {
                        Position = new Vector2(18, -6),
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Text = s.Count.ToString(@"N0"),
                        Font = OsuFont.Torus.With(size: 38, weight: FontWeight.Regular),
                        Colour = ColourInfo.GradientVertical(Interpolation(c, Color4.White, 0.35f), c),
                    },
                },
            };
        }

        /// <summary>The score as big as the mockup draws it (light, with a cool gradient).</summary>
        private partial class BigScoreCounter : TotalScoreCounter
        {
            public BigScoreCounter(bool playSamples)
                : base(playSamples)
            {
            }

            protected override OsuSpriteText CreateSpriteText() => base.CreateSpriteText().With(s =>
            {
                s.Font = OsuFont.Torus.With(size: 150, weight: FontWeight.Light, fixedWidth: true);
                s.Spacing = new Vector2(-10, 0);
                s.Colour = ColourInfo.GradientVertical(Color4.White, Color4Extensions.FromHex(@"cfeaff"));
            });
        }

        private static Color4 Interpolation(Color4 a, Color4 b, float t) => new Color4(
            a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, 1);
    }
}
