// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// TumThai: the Rev 11 mockups' "Advanced Info Screen": the beatmap header with its numbers, the player row, the score
    /// with grade, mods and multiplier, the statistics and judgement columns, the hit error and accuracy-over-time graphs,
    /// and panels for the performance breakdown, timing distribution and accuracy heatmap (from the ruleset, with the
    /// play's hit events). Laid out on the mockup's 1920x1080 and scaled.
    /// </summary>
    public partial class Rev11AdvancedPanel : CompositeDrawable
    {
        private static readonly Color4 panel_colour = Color4Extensions.FromHex(@"161a1d").Opacity(0.94f);
        private static readonly Color4 frame_colour = Color4Extensions.FromHex(@"242a2f").Opacity(0.92f);

        private readonly ScoreInfo score;

        /// <summary>Back to the score overview (the "← Ranking" button).</summary>
        public Action? BackAction;

        private Container hitErrorGraph = null!;
        private Container accuracyGraph = null!;
        private readonly Container[] bottomPanels = new Container[4];
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private RealmAccess? realm { get; set; }

        /// <summary>This player's best earlier score on the map (same ruleset, saved locally), for the "+x" differences.</summary>
        private ScoreInfo? previousBest;

        public Rev11AdvancedPanel(ScoreInfo score)
        {
            this.score = score;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var beatmap = score.BeatmapInfo!;
            var working = beatmaps.GetWorkingBeatmap(beatmap);
            var stats = score.GetStatisticsForDisplay().ToList();
            previousBest = findPreviousBest();

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
                        // the frame behind everything below the header
                        rounded(new Vector2(90, 178), new Vector2(1805, 798), frame_colour, 14),
                        header(beatmap, working),
                        playerRow(),
                        accuracyBar(),
                        scoreRow(),
                        // statistics column
                        rounded(new Vector2(110, 352), new Vector2(160, 334), panel_colour, 8).With(c => ((Container)c).Add(statColumn(stats))),
                        // judgements column
                        rounded(new Vector2(276, 352), new Vector2(110, 334), panel_colour, 8).With(c => ((Container)c).Add(judgementColumn(stats))),
                        // timing numbers
                        rounded(new Vector2(110, 692), new Vector2(276, 114), panel_colour, 8).With(c => ((Container)c).Add(timingNumbers())),
                        // the graphs
                        graphPanel(new Vector2(392, 352), new Vector2(1476, 256), @"Hit Error", out hitErrorGraph),
                        graphPanel(new Vector2(392, 616), new Vector2(1476, 190), @"Accuracy", out accuracyGraph),
                        // the four panels along the bottom
                        bottomPanel(0, @"Performance Breakdown"),
                        bottomPanel(1, @"Beatmap Tags"),
                        bottomPanel(2, @"Timing Distribution"),
                        bottomPanel(3, @"Accuracy Heatmap"),
                    },
                },
            };

            drawGraphs();

            // the ruleset's statistics need the playable beatmap (and the hit events for most)
            Task.Run(() => working.GetPlayableBeatmap(score.Ruleset, score.Mods), cancellation.Token).ContinueWith(t => Schedule(() =>
            {
                if (t.IsCanceled || t.IsFaulted || cancellation.IsCancellationRequested)
                    return;

                var items = score.Ruleset.CreateInstance().CreateStatisticsForScore(score, t.GetResultSafely());
                fill(0, items.FirstOrDefault(i => i.Name.ToString().Contains("Performance", StringComparison.OrdinalIgnoreCase)));
                fillTags();
                fill(2, items.FirstOrDefault(i => i.Name.ToString().Contains("Timing", StringComparison.OrdinalIgnoreCase)));
                fill(3, items.FirstOrDefault(i => i.Name.ToString().Contains("Heatmap", StringComparison.OrdinalIgnoreCase)));
            }), cancellation.Token);
        }

        private void fill(int panel, StatisticItem? item)
        {
            if (item == null || (item.RequiresHitEvents && score.HitEvents.Count == 0))
            {
                bottomPanels[panel].Add(note(score.HitEvents.Count == 0 ? @"Watch the replay to see this." : @"Not for this mode."));
                return;
            }

            bottomPanels[panel].Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Top = 6 },
                Child = item.CreateContent().With(d =>
                {
                    d.Anchor = Anchor.Centre;
                    d.Origin = Anchor.Centre;
                }),
            });
        }

        private void fillTags() => bottomPanels[1].Add(new UserTagControl(score.BeatmapInfo!)
        {
            RelativeSizeAxes = Axes.X,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Writable = true,
        });

        private Drawable header(BeatmapInfo beatmap, WorkingBeatmap working)
        {
            var d = beatmap.Difficulty;
            int circles = 0, sliders = 0, spinners = 0;

            try
            {
                foreach (var o in working.Beatmap.HitObjects)
                {
                    string type = o.GetType().Name;
                    if (type.Contains("Slider", StringComparison.Ordinal)) sliders++;
                    else if (type.Contains("Spinner", StringComparison.Ordinal)) spinners++;
                    else circles++;
                }
            }
            catch
            {
                // counts are nice to have
            }

            return new Container
            {
                Position = new Vector2(90, 85),
                Size = new Vector2(1805, 90),
                CornerRadius = 14,
                Masking = true,
                BorderThickness = 3,
                BorderColour = ColourInfo.GradientHorizontal(Color4Extensions.FromHex(@"ff99cc").Opacity(0.6f), Color4Extensions.FromHex(@"ff99cc")),
                Children = new Drawable[]
                {
                    new BeatmapBackgroundSprite(working)
                    {
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fill,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.55f), Color4.Black.Opacity(0.2f)),
                    },
                    new FillFlowContainer
                    {
                        Position = new Vector2(22, 12),
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new BeatmapSetOnlineStatusPill { Status = beatmap.Status, TextSize = 10 },
                            new TruncatingSpriteText
                            {
                                Text = new RomanisableString(beatmap.Metadata.TitleUnicode, beatmap.Metadata.Title),
                                Font = OsuFont.Torus.With(size: 24, weight: FontWeight.SemiBold),
                                MaxWidth = 900,
                            },
                            new TruncatingSpriteText
                            {
                                Text = new RomanisableString(beatmap.Metadata.ArtistUnicode, beatmap.Metadata.Artist),
                                Font = OsuFont.Torus.With(size: 15, weight: FontWeight.SemiBold),
                                MaxWidth = 900,
                            },
                        },
                    },
                    // the difficulty's side
                    new Container
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Size = new Vector2(745, 90),
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex(@"161a1d").Opacity(0.9f) },
                            new FillFlowContainer
                            {
                                Position = new Vector2(20, 10),
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(10, 0),
                                Children = new Drawable[]
                                {
                                    new StarRatingDisplay(new StarDifficulty(beatmap.StarRating, 0), StarRatingDisplaySize.Small),
                                    new OsuSpriteText
                                    {
                                        Text = beatmap.DifficultyName,
                                        Font = OsuFont.Torus.With(size: 15, weight: FontWeight.Bold),
                                        Colour = colours.ForStarDifficulty(beatmap.StarRating),
                                    },
                                    new OsuSpriteText
                                    {
                                        Text = $"mapped by {beatmap.Metadata.Author.Username}",
                                        Font = OsuFont.Torus.With(size: 13, weight: FontWeight.SemiBold),
                                        Colour = colours.ForStarDifficulty(beatmap.StarRating).Opacity(0.85f),
                                    },
                                },
                            },
                            new FillFlowContainer
                            {
                                Position = new Vector2(20, 44),
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(14, 0),
                                Children = new[]
                                {
                                    numberCell(@"Circles", circles.ToString()),
                                    numberCell(@"Sliders", sliders.ToString()),
                                    numberCell(@"Spinners", spinners.ToString()),
                                    numberCell(@"Circle Size", $"{d.CircleSize:0.#}"),
                                    numberCell(@"Approach Rate", $"{d.ApproachRate:0.#}"),
                                    numberCell(@"Accuracy", $"{d.OverallDifficulty:0.#}"),
                                    numberCell(@"HP Drain", $"{d.DrainRate:0.#}"),
                                },
                            },
                        },
                    },
                },
            };
        }

        private Drawable numberCell(string name, string value) => new Container
        {
            Size = new Vector2(84, 36),
            Children = new Drawable[]
            {
                new Box { Size = new Vector2(30, 2), Colour = Color4Extensions.FromHex(@"ff9966") },
                new OsuSpriteText { Y = 6, Text = name, Font = OsuFont.Torus.With(size: 11, weight: FontWeight.SemiBold) },
                new OsuSpriteText { Y = 19, Text = value, Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold) },
            },
        };

        private Drawable playerRow() => new Container
        {
            Position = new Vector2(110, 185),
            Size = new Vector2(1765, 56),
            Children = new Drawable[]
            {
                new RoundedButton
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(108, 34),
                    Text = @"← Ranking",
                    Action = () => BackAction?.Invoke(),
                },
                new FillFlowContainer
                {
                    X = 150,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(12, 0),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = score.Position is int p ? $"#{p:N0}" : @"-",
                            Font = OsuFont.Torus.With(size: 30, weight: FontWeight.Regular),
                        },
                        new UpdateableAvatar(score.User, showUserPanelOnHover: true)
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(50),
                            CornerRadius = 6,
                            Masking = true,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = score.User.Username,
                            Font = OsuFont.Torus.With(size: 22, weight: FontWeight.SemiBold),
                        },
                        new UpdateableFlag(score.User.CountryCode)
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(30, 20),
                        },
                    },
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(24, 0),
                    Children = new[]
                    {
                        labelled(@"Achieved on", score.Date.ToLocalTime().ToString(@"yyyy/MM/dd HH:mm")),
                    },
                },
            },
        };

        private static Drawable labelled(string label, string value) => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Children = new Drawable[]
            {
                new OsuSpriteText { Text = label, Font = OsuFont.Torus.With(size: 12, weight: FontWeight.SemiBold), Colour = Color4.White.Opacity(0.8f) },
                new OsuSpriteText { Text = value, Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold) },
            },
        };

        private Drawable accuracyBar()
        {
            // the accuracy as a bar, with the grades' bands under it
            var bands = new[] { (ScoreRank.D, 0.0), (ScoreRank.C, 0.7), (ScoreRank.B, 0.8), (ScoreRank.A, 0.9), (ScoreRank.S, 0.95), (ScoreRank.X, 1.0) };

            var flow = new Container { Position = new Vector2(110, 248), Size = new Vector2(1760, 14) };
            flow.Add(new Circle { Size = new Vector2(1760, 6), Colour = Color4.White.Opacity(0.12f) });
            flow.Add(new Circle
            {
                Size = new Vector2(1760 * (float)Math.Clamp(score.Accuracy, 0, 1), 6),
                Colour = ColourInfo.GradientHorizontal(Color4Extensions.FromHex(@"a8f7c7"), Color4Extensions.FromHex(@"7ef1ff")),
            });

            for (int i = 0; i < bands.Length - 1; i++)
            {
                float start = (float)bands[i].Item2, end = (float)bands[i + 1].Item2;
                flow.Add(new Circle
                {
                    X = 1760 * start + 2,
                    Y = 10,
                    Size = new Vector2(1760 * (end - start) - 4, 3),
                    Colour = OsuColour.ForRank(bands[i].Item1),
                });
            }

            return flow;
        }

        private Drawable scoreRow()
        {
            double multiplier = 1;

            try
            {
                multiplier = score.Ruleset.CreateInstance().CreateScoreMultiplierCalculator(new ScoreMultiplierContext(score.BeatmapInfo!.Difficulty, score)).CalculateFor(score.Mods);
            }
            catch
            {
                // 1x
            }

            return new Container
            {
                Position = new Vector2(110, 268),
                Size = new Vector2(1760, 70),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(14, 0),
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(80, 70),
                                Child = new V2RankEmblem(score.Rank, rev11Colours: true)
                                {
                                    AutoAppear = true,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Scale = new Vector2(0.28f),
                                },
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = score.TotalScore.ToString(@"N0"),
                                Font = OsuFont.Torus.With(size: 64, weight: FontWeight.Light, fixedWidth: true),
                                Spacing = new Vector2(-4, 0),
                                Colour = ColourInfo.GradientVertical(Color4.White, Color4Extensions.FromHex(@"cfeaff")),
                            },
                            deltaText(previousBest == null ? null : score.TotalScore - previousBest.TotalScore, @"#,0", 20).With(d =>
                            {
                                d.Anchor = Anchor.CentreLeft;
                                d.Origin = Anchor.CentreLeft;
                            }),
                        },
                    },
                    new Container
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Size = new Vector2(420, 62),
                        CornerRadius = 8,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = panel_colour },
                            new OsuSpriteText { Position = new Vector2(14, 6), Text = @"MODS", Font = OsuFont.Torus.With(size: 14, weight: FontWeight.Bold) },
                            new ModFlowDisplay
                            {
                                Position = new Vector2(14, 26),
                                AutoSizeAxes = Axes.Both,
                                Scale = new Vector2(0.45f),
                                Current = { Value = score.Mods },
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Position = new Vector2(-14, 6),
                                Text = @"MULTIPLIER",
                                Font = OsuFont.Torus.With(size: 14, weight: FontWeight.Bold),
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Position = new Vector2(-14, 28),
                                Text = $"{multiplier:0.##}x",
                                Font = OsuFont.Torus.With(size: 18, weight: FontWeight.Bold),
                                Colour = Color4Extensions.FromHex(@"ff8899"),
                            },
                        },
                    },
                },
            };
        }

        private Drawable statColumn(List<HitResultDisplayStatistic> stats)
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(12),
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
            };

            var best = previousBest;
            flow.Add(statLine(@"ACCURACY", score.DisplayAccuracy.ToString(), best == null ? null : (score.Accuracy - best.Accuracy) * 100, @"0.00'%'"));
            flow.Add(statLine(@"COMBO", $"{score.MaxCombo}x", best == null ? null : score.MaxCombo - best.MaxCombo, @"0'x'"));
            flow.Add(statLine(@"PP", score.PP is double pp ? $"{pp:N0}" : @"-", score.PP is double p1 && best?.PP is double p0 ? p1 - p0 : null, @"0"));

            foreach (var s in stats.Where(s => !s.Result.IsBasic()).Take(3))
                flow.Add(statLine(s.DisplayName.ToString().ToUpperInvariant(), s.Count.ToString(@"N0")));

            return flow;
        }

        private static Drawable statLine(string header, string value, double? delta = null, string deltaFormat = @"0")
        {
            var line = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new OsuSpriteText { Text = header, Font = OsuFont.Torus.With(size: 13, weight: FontWeight.Bold) },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(6, 0),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText { Text = value, Font = OsuFont.Torus.With(size: 22, weight: FontWeight.Regular) },
                            deltaText(delta, deltaFormat, 13).With(d =>
                            {
                                d.Anchor = Anchor.BottomLeft;
                                d.Origin = Anchor.BottomLeft;
                                d.Margin = new MarginPadding { Bottom = 3 };
                            }),
                        },
                    },
                },
            };
            return line;
        }

        /// <summary>"+1.23" in green, "-1.23" in red, nothing when there's no earlier score or no difference.</summary>
        private static Drawable deltaText(double? delta, string format, float size)
        {
            if (delta is not double d || Math.Abs(d) < 1e-9)
                return Empty();

            return new OsuSpriteText
            {
                Text = (d > 0 ? @"+" : @"") + d.ToString(format, System.Globalization.CultureInfo.InvariantCulture),
                Font = OsuFont.Torus.With(size: size, weight: FontWeight.SemiBold),
                Colour = d > 0 ? Color4Extensions.FromHex(@"88ee66") : Color4Extensions.FromHex(@"ff6677"),
            };
        }

        private ScoreInfo? findPreviousBest()
        {
            if (realm == null || score.BeatmapInfo == null)
                return null;

            try
            {
                Guid beatmapId = score.BeatmapInfo.ID;
                int userId = score.User.OnlineID;
                string ruleset = score.Ruleset.ShortName;

                return realm.Run(r => r.All<ScoreInfo>()
                                       .Where(s => !s.DeletePending && s.BeatmapInfo != null && s.BeatmapInfo.ID == beatmapId)
                                       .ToList()
                                       .Where(s => s.ID != score.ID && s.User.OnlineID == userId && s.Ruleset.ShortName == ruleset && s.Date < score.Date)
                                       .OrderByDescending(s => s.TotalScore)
                                       .FirstOrDefault()?.Detach());
            }
            catch
            {
                // the differences are nice to have
                return null;
            }
        }

        private Drawable judgementColumn(List<HitResultDisplayStatistic> stats)
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(12),
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
            };

            foreach (var s in stats.Where(s => s.Result.IsBasic()))
            {
                var c = colours.ForHitResult(s.Result);

                flow.Add(new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            AutoSizeAxes = Axes.Both,
                            CornerRadius = 3,
                            Masking = true,
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Colour = c },
                                new OsuSpriteText
                                {
                                    Margin = new MarginPadding { Horizontal = 4 },
                                    Text = s.DisplayName.ToString().ToUpperInvariant(),
                                    Font = OsuFont.Torus.With(size: 12, weight: FontWeight.Bold),
                                    Colour = Color4.Black,
                                },
                            },
                        },
                        new OsuSpriteText { Text = s.Count.ToString(@"N0"), Font = OsuFont.Torus.With(size: 22, weight: FontWeight.Regular) },
                    },
                });
            }

            return flow;
        }

        private Drawable timingNumbers()
        {
            var events = score.HitEvents.Where(HitEventExtensions.AffectsUnstableRate).ToList();
            double? ur = score.HitEvents.CalculateUnstableRate()?.Result;
            double? mean = score.HitEvents.CalculateAverageHitError();
            double early = events.Count > 0 ? events.Where(e => e.TimeOffset < 0).Select(e => e.TimeOffset).DefaultIfEmpty(0).Average() : 0;
            double late = events.Count > 0 ? events.Where(e => e.TimeOffset > 0).Select(e => e.TimeOffset).DefaultIfEmpty(0).Average() : 0;

            string ms(double? v) => v is double d ? $"{d:0.#}ms" : @"-";

            return new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(12),
                Content = new[]
                {
                    new[] { statLine(@"UR", ur is double u ? $"{u:0.#}" : @"-"), statLine(@"MEAN", ms(mean)), Empty() },
                    new[] { statLine(@"EARLY", events.Count > 0 ? ms(early) : @"-"), statLine(@"LATE", events.Count > 0 ? ms(late) : @"-"), Empty() },
                },
            };
        }

        private Drawable graphPanel(Vector2 position, Vector2 size, string title, out Container content)
        {
            var panel = (Container)rounded(position, size, panel_colour, 8);
            panel.Add(new FillFlowContainer
            {
                Position = new Vector2(16, 12),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Children = new Drawable[]
                {
                    new Box { Size = new Vector2(3, 16), Colour = Color4Extensions.FromHex(@"66ffcc") },
                    new OsuSpriteText { Text = title, Font = OsuFont.Torus.With(size: 15, weight: FontWeight.SemiBold) },
                },
            });
            panel.Add(content = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Top = 40, Bottom = 14, Horizontal = 18 },
            });
            return panel;
        }

        private void drawGraphs()
        {
            var events = score.HitEvents.Where(e => e.HitObject != null).ToList();

            if (events.Count == 0)
            {
                hitErrorGraph.Add(note(@"Watch the replay to see the hit errors."));
                accuracyGraph.Add(note(@"Watch the replay to see accuracy over time."));
                return;
            }

            double start = events.Min(e => e.HitObject.StartTime);
            double end = Math.Max(start + 1, events.Max(e => e.HitObject.StartTime));
            var timed = events.Where(HitEventExtensions.AffectsUnstableRate).ToList();
            double range = Math.Max(20, timed.Count > 0 ? timed.Max(e => Math.Abs(e.TimeOffset)) : 20);

            // hit error over time: a dot per hit, its offset up (late) or down (early); misses red along the top
            var dots = new Container { RelativeSizeAxes = Axes.Both };
            dots.Add(new Box { RelativeSizeAxes = Axes.X, Height = 1, RelativePositionAxes = Axes.Y, Y = 0.5f, Colour = Color4.White.Opacity(0.3f) });

            foreach (var e in events)
            {
                bool miss = !e.Result.IsHit();
                if (!miss && !HitEventExtensions.AffectsUnstableRate(e))
                    continue;

                dots.Add(new Circle
                {
                    RelativePositionAxes = Axes.Both,
                    Origin = Anchor.Centre,
                    X = (float)((e.HitObject.StartTime - start) / (end - start)),
                    Y = miss ? 0.02f : (float)(0.5 - 0.48 * Math.Clamp(e.TimeOffset / range, -1, 1)),
                    Size = new Vector2(miss ? 4 : 3),
                    Colour = colours.ForHitResult(e.Result),
                });
            }

            hitErrorGraph.Add(dots);

            // accuracy over time: the running accuracy of the judgements that count towards it
            var processorRuleset = score.Ruleset.CreateInstance();
            using var processor = processorRuleset.CreateScoreProcessor();
            double baseSum = 0, maxSum = 0;
            var points = new List<(float x, float y)>();

            foreach (var e in events.Where(e => e.Result.AffectsAccuracy()))
            {
                baseSum += processor.GetBaseScoreForResult(e.Result);
                maxSum += processor.GetBaseScoreForResult(e.HitObject.Judgement.MaxResult);
                double acc = maxSum > 0 ? baseSum / maxSum : 1;
                points.Add(((float)((e.HitObject.StartTime - start) / (end - start)), (float)acc));
            }

            var line = new Container { RelativeSizeAxes = Axes.Both };

            foreach (float level in new[] { 1f, 0.5f, 0f })
                line.Add(new Box { RelativeSizeAxes = Axes.X, Height = 1, RelativePositionAxes = Axes.Y, Y = 1 - level, Colour = Color4.White.Opacity(0.15f) });

            int step = Math.Max(1, points.Count / 600);

            for (int i = 0; i < points.Count; i += step)
            {
                var (x, y) = points[i];
                line.Add(new Circle
                {
                    RelativePositionAxes = Axes.Both,
                    Origin = Anchor.Centre,
                    X = x,
                    Y = 1 - Math.Clamp(y, 0, 1),
                    Size = new Vector2(3),
                    Colour = Interpolate(Color4Extensions.FromHex(@"ff5555"), Color4Extensions.FromHex(@"b3d943"), (float)Math.Clamp((y - 0.8) / 0.2, 0, 1)),
                });
            }

            accuracyGraph.Add(line);
        }

        private static Color4 Interpolate(Color4 a, Color4 b, float t) => new Color4(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, 1);

        private Drawable bottomPanel(int index, string title)
        {
            float x = 110 + index * 443;
            var panel = (Container)rounded(new Vector2(x, 823), new Vector2(435, 146), panel_colour, 8);
            panel.Add(new FillFlowContainer
            {
                Position = new Vector2(14, 10),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Children = new Drawable[]
                {
                    new Box { Size = new Vector2(3, 14), Colour = Color4Extensions.FromHex(@"66ffcc") },
                    new OsuSpriteText { Text = title, Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold) },
                },
            });
            panel.Add(bottomPanels[index] = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Top = 30, Bottom = 8, Horizontal = 10 },
            });
            return panel;
        }

        private static Drawable note(string text) => new OsuSpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = text,
            Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold),
            Colour = Color4.White.Opacity(0.6f),
        };

        private static Drawable rounded(Vector2 position, Vector2 size, ColourInfo colour, float radius) => new Container
        {
            Position = position,
            Size = size,
            CornerRadius = radius,
            Masking = true,
            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
        };

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
