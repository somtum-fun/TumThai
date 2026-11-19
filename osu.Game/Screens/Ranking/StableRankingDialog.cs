// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Extensions;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Performance;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// TumThai!Next: osu!stable after-play Ranking Chart / Score Submission dialog (Page 2 of the Ranking screen),
    /// faithfully matching <c>osu.GameModes.Ranking.RankingDialog</c> using the 3-slice <c>ranking-dialog-left/middle/right</c>
    /// background at <c>Y = 125f</c> (LOCAL, ONLINE 6-column comparison table, and BEATMAP info).
    /// TumThai: no beatmap rating stars (there is nothing to send a rating to).
    /// </summary>
    public partial class StableRankingDialog : VisibilityContainer
    {
        private readonly Action onClose;
        private readonly Action? onOpenOnlineScoreboard;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private RealmAccess? realm { get; set; }

        [Resolved(canBeNull: true)]
        private ScoreManager? scoreManager { get; set; }

        [Resolved(canBeNull: true)]
        private UserStatisticsWatcher? userStatisticsWatcher { get; set; }

        private readonly IBindable<ScoreBasedUserStatisticsUpdate?> latestStatisticsUpdate = new Bindable<ScoreBasedUserStatisticsUpdate?>();
        private ScoreBasedUserStatisticsUpdate? statisticsUpdate;

        // The server's view of this score on the beatmap's leaderboard, once it has processed it.
        private bool beatmapRankingRequested;
        private bool beatmapRankingDone;
        private int? onlineRank;
        private double? onlinePp;
        private bool isOnlineBest;
        private GetScoresRequest? beatmapRankingRequest;

        private OsuSpriteText localScoreDiffText = null!;
        private OsuSpriteText localRankHeadlineText = null!;
        private StableOptionRadioRow saveReplayRow = null!;
        private StableOptionRadioRow addFavouriteRow = null!;
        private Sprite modeWatermarkSprite = null!;

        private ComparisonCell[] beatmapRowCells = null!;
        private ComparisonCell[] overallRowCells = null!;

        private OsuSpriteText beatmapHeaderLine1 = null!;
        private OsuSpriteText beatmapHeaderLine2 = null!;

        private ScoreInfo? currentScore;
        private ISample? swooshSample;

        private static readonly Color4 colour_improved_green = new Color4(103, 157, 17, 255);
        private static readonly Color4 colour_cell_row_1 = new Color4(40, 40, 40, 250);
        private static readonly Color4 colour_cell_row_2 = new Color4(20, 20, 20, 250);

        private static readonly float[] column_widths =
        {
            112f, // Overall (70 * 1.6)
            112f, // Accuracy (70 * 1.6)
            112f, // Max Combo (70 * 1.6)
            176f, // Ranked Score (110 * 1.6)
            176f, // Total Score (110 * 1.6)
            112f, // Performance (70 * 1.6)
        };

        private const float row_header_width = 160f; // 100 * 1.6
        private const float row_height = 38f; // 24 * 1.6
        private const float cell_h_spacing = 2f;
        private const float cell_v_spacing = 3f;

        public StableRankingDialog(Action onClose, Action? onOpenOnlineScoreboard)
        {
            this.onClose = onClose;
            this.onOpenOnlineScoreboard = onOpenOnlineScoreboard;

            RelativeSizeAxes = Axes.Both;
            Y = 672f;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            var classicSkin = skinManager.DefaultClassicSkin;
            swooshSample = skinManager.GetClassicSample(@"menuhit") ?? audio.Samples.Get(@"UI/overlay-pop-in");

            var leftTex = classicSkin.GetTexture(@"ranking-dialog-left");
            var midTex = classicSkin.GetTexture(@"ranking-dialog-middle");
            var rightTex = classicSkin.GetTexture(@"ranking-dialog-right");

            beatmapRowCells = new ComparisonCell[6];
            overallRowCells = new ComparisonCell[6];

            string[] columnHeaders =
            {
                "Overall",
                "Accuracy",
                "Max Combo",
                "Ranked Score",
                "Total Score",
                "Performance",
            };

            var onlineHeaderRow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(cell_h_spacing, 0f),
                Children = new Drawable[]
                {
                    new Container { Size = new Vector2(row_header_width, 20f) },
                },
            };

            for (int i = 0; i < columnHeaders.Length; i++)
            {
                onlineHeaderRow.Add(new Container
                {
                    Size = new Vector2(column_widths[i], 20f),
                    Child = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = columnHeaders[i],
                        Font = OsuFont.GetFont(size: 16f, weight: FontWeight.Regular),
                        Colour = Color4.White,
                        Shadow = true,
                    },
                });
            }

            var beatmapCellsFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(cell_h_spacing, 0f),
                Children = new Drawable[]
                {
                    createRowHeaderCell("Beatmap Ranking", colour_cell_row_1),
                },
            };

            var overallCellsFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(cell_h_spacing, 0f),
                Children = new Drawable[]
                {
                    createRowHeaderCell("Overall Ranking", colour_cell_row_2),
                },
            };

            for (int i = 0; i < 6; i++)
            {
                beatmapRowCells[i] = new ComparisonCell(column_widths[i], colour_cell_row_1);
                overallRowCells[i] = new ComparisonCell(column_widths[i], colour_cell_row_2);
                beatmapCellsFlow.Add(beatmapRowCells[i]);
                overallCellsFlow.Add(overallRowCells[i]);
            }

            const float bg_alpha = 220f / 255f;

            InternalChildren = new Drawable[]
            {
                // Top 29px gap (Y = 96..125) above ranking-dialog: clicking it returns to Page 1
                new ClickableContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Y = 96f,
                    Height = 29f,
                    Action = onClose,
                },
                // Main ranking-dialog body starting at Y = 125f (matching RankingDialog.cs lines 392-400)
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Y = 125f,
                    Height = 709f,
                    Children = new Drawable[]
                    {
                        // 3-slice background (ranking-dialog-left, ranking-dialog-middle, ranking-dialog-right)
                        leftTex != null && midTex != null && rightTex != null
                            ? new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    new Sprite
                                    {
                                        Anchor = Anchor.TopLeft,
                                        Origin = Anchor.TopLeft,
                                        Texture = leftTex,
                                        Alpha = bg_alpha,
                                    },
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = midTex.DisplayHeight,
                                        Padding = new MarginPadding
                                        {
                                            Left = leftTex.DisplayWidth,
                                            Right = rightTex.DisplayWidth,
                                        },
                                        Child = new Sprite
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            FillMode = FillMode.Stretch,
                                            Texture = midTex,
                                            Alpha = bg_alpha,
                                        },
                                    },
                                    new Sprite
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        Texture = rightTex,
                                        Alpha = bg_alpha,
                                    },
                                },
                            }
                            : new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.Black.Opacity(0.85f),
                            },

                        // === BAND 1: LOCAL (y = 0..205) ===
                        localScoreDiffText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Y = 2f,
                            Font = OsuFont.GetFont(size: 13.5f, weight: FontWeight.Bold),
                            Colour = new Color4(163, 240, 44, 255),
                            Shadow = true,
                        },
                        localRankHeadlineText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.Centre,
                            Y = 29f,
                            Font = OsuFont.GetFont(size: 27f, weight: FontWeight.Regular),
                            Colour = new Color4(255, 203, 33, 255),
                            Shadow = true,
                        },
                        new StableUserPanel
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Y = 86f,
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopLeft,
                            Position = new Vector2(-264f, 16f),
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0f, 10f),
                            Children = new Drawable[]
                            {
                                saveReplayRow = new StableOptionRadioRow("Save replay to Replays folder", () =>
                                {
                                    if (currentScore != null)
                                        scoreManager?.Export(currentScore);
                                }),
                                addFavouriteRow = new StableOptionRadioRow("Add as online favourite", addFavourite),
                            },
                        },

                        // === BAND 2: ONLINE (y = 205..478) ===
                        modeWatermarkSprite = new Sprite
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.Centre,
                            Position = new Vector2(-160f, 368f),
                            Scale = new Vector2(0.8f),
                            Rotation = 6f,
                            Alpha = 0.1f,
                            Blending = BlendingParameters.Additive,
                            Texture = classicSkin.GetTexture(@"mode-osu"),
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Y = 263f,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0f, cell_v_spacing),
                            Children = new Drawable[]
                            {
                                onlineHeaderRow,
                                beatmapCellsFlow,
                                overallCellsFlow,
                            },
                        },

                        // === BAND 3: BEATMAP (y = 478..643) ===
                        beatmapHeaderLine1 = new OsuSpriteText
                        {
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.TopLeft,
                            Position = new Vector2(272f, 480f),
                            Font = OsuFont.GetFont(size: 21f, weight: FontWeight.Regular),
                            Colour = Color4.White,
                            Shadow = true,
                        },
                        beatmapHeaderLine2 = new OsuSpriteText
                        {
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.TopLeft,
                            Position = new Vector2(272f, 505f),
                            Font = OsuFont.GetFont(size: 16.5f, weight: FontWeight.Regular),
                            Colour = Color4.White,
                            Shadow = true,
                        },
                    },
                },
                // Bottom-center button to scroll back up to Page 1 (Score Details)
                new StableThreeSliceButton
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -6f,
                    Size = new Vector2(280f, 36f),
                    BackgroundColour = Color4.BlueViolet,
                    Text = "▲ Score Details ▲",
                    FontSize = 18f,
                    Action = onClose,
                },
                // Bottom-right button to open the full Scoreboard
                new StableThreeSliceButton
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Position = new Vector2(-16f, -6f),
                    Size = new Vector2(200f, 36f),
                    BackgroundColour = Color4Extensions.FromHex(@"2A4E66"),
                    Text = "Scoreboard",
                    FontSize = 16f,
                    Action = () => onOpenOnlineScoreboard?.Invoke(),
                },
            };
        }

        private static Drawable createRowHeaderCell(string label, Color4 bgColour)
        {
            return new Container
            {
                Size = new Vector2(row_header_width, row_height),
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = bgColour,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = label,
                        Font = OsuFont.GetFont(size: 16.5f, weight: FontWeight.Bold),
                        Colour = Color4.Orange,
                        Shadow = true,
                    },
                },
            };
        }

        public void SetScore(ScoreInfo score)
        {
            currentScore = score;
            if (!IsLoaded)
                return;

            populateFromScore(score);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (userStatisticsWatcher != null)
            {
                latestStatisticsUpdate.BindTo(userStatisticsWatcher.LatestUpdate);
                latestStatisticsUpdate.BindValueChanged(u =>
                {
                    if (currentScore == null || u.NewValue?.Score.MatchesOnlineID(currentScore) != true)
                        return;

                    statisticsUpdate = u.NewValue;
                    populateOverallRow();

                    // the server has processed the score: its rank and pp on the beatmap are there now
                    requestBeatmapRanking();
                }, true);
            }

            // without word from the server (no spectator connection), ask anyway after a while
            Scheduler.AddDelayed(requestBeatmapRanking, 15000);

            if (currentScore != null)
                populateFromScore(currentScore);
        }

        private void populateFromScore(ScoreInfo score)
        {
            saveReplayRow.SetChecked(false);
            addFavouriteRow.SetChecked(false);

            string modeTexName = score.Ruleset.ShortName switch
            {
                @"taiko" => @"mode-taiko",
                @"fruits" => @"mode-fruits",
                @"mania" => @"mode-mania",
                _ => @"mode-osu",
            };
            modeWatermarkSprite.Texture = skinManager.GetClassicTexture(modeTexName);

            var beatmap = score.BeatmapInfo;
            var meta = beatmap?.Metadata;
            string artist = meta?.Artist ?? string.Empty;
            string title = meta?.Title ?? string.Empty;
            string diff = beatmap?.DifficultyName ?? string.Empty;
            string creator = meta?.Author.Username ?? string.Empty;

            beatmapHeaderLine1.Text = !string.IsNullOrEmpty(artist)
                ? $"{artist} - {title} [{diff}] by {creator}"
                : $"{title} [{diff}] by {creator}";

            int playCount = Math.Max(1, beatmap?.OnlineInfo?.PlayCount ?? 1);
            int passCount = Math.Max(score.Passed ? 1 : 0, beatmap?.OnlineInfo?.PassCount ?? 1);
            double passRate = playCount > 0 ? Math.Clamp(100.0 * passCount / playCount, 0.0, 100.0) : 100.0;
            beatmapHeaderLine2.Text = $"{playCount:N0} plays | {passRate:0}% pass rate";

            // Query local scores for this beatmap from Realm to compute local rank and previous personal best
            int localRank = 1;
            ScoreInfo? prevBest = null;

            if (realm != null && beatmap != null)
            {
                try
                {
                    var localScores = realm.Run(r => r.All<ScoreInfo>()
                                                      .Where(s => !s.DeletePending && s.BeatmapInfo != null && s.BeatmapInfo.ID == beatmap.ID)
                                                      .ToList()
                                                      .Select(s => s.Detach())
                                                      .OrderByDescending(s => s.TotalScore)
                                                      .ToList());

                    int higherCount = localScores.Count(s => s.ID != score.ID && s.TotalScore > score.TotalScore);
                    localRank = higherCount + 1;

                    prevBest = localScores.FirstOrDefault(s => s.ID != score.ID);
                }
                catch
                {
                    localRank = 1;
                }
            }

            if (prevBest != null && score.TotalScore > prevBest.TotalScore)
                localScoreDiffText.Text = $"+{score.TotalScore - prevBest.TotalScore:N0}";
            else
                localScoreDiffText.Text = string.Empty;

            localRankHeadlineText.Text = $"You achieved the #{localRank} score on local rankings!";

            // Populate Row 1: Beatmap Ranking. Rank and pp are the server's, "Pending..." until it has processed the score.
            populateBeatmapServerCells();

            double acc = score.Accuracy * 100.0;
            double prevAcc = (prevBest?.Accuracy ?? 0) * 100.0;
            bool accImproved = prevBest == null || acc >= prevAcc - 0.001;
            double accDiff = prevBest != null ? acc - prevAcc : acc;
            beatmapRowCells[1].SetContent(
                $"{acc:0.00}%",
                accImproved ? $"(+{Math.Max(0, accDiff):0.00}%)" : $"your best: {prevAcc:0.00}%",
                accImproved);

            int combo = score.MaxCombo;
            int prevCombo = prevBest?.MaxCombo ?? 0;
            bool comboImproved = prevBest == null || combo >= prevCombo;
            beatmapRowCells[2].SetContent(
                $"{combo:N0}x",
                comboImproved ? $"(+{Math.Max(0, combo - prevCombo):N0}x)" : $"your best: {prevCombo:N0}x",
                comboImproved);

            long totalScore = score.TotalScore;
            long prevScore = prevBest?.TotalScore ?? 0;
            bool scoreImproved = prevBest == null || totalScore >= prevScore;
            beatmapRowCells[3].SetContent(
                $"{totalScore:N0}",
                scoreImproved ? $"(+{Math.Max(0, totalScore - prevScore):N0})" : $"your best: {prevScore:N0}",
                scoreImproved);

            beatmapRowCells[4].SetContent(
                $"{totalScore:N0}",
                "-",
                false);

            populateOverallRow();
        }

        private void requestBeatmapRanking()
        {
            if (beatmapRankingRequested || currentScore?.BeatmapInfo == null || currentScore.OnlineID <= 0 || !api.IsLoggedIn)
                return;

            beatmapRankingRequested = true;

            var score = currentScore;
            var variant = score.Mods.Any(m => m.Acronym == @"RX") ? LeaderboardVariant.Relax
                : score.Mods.Any(m => m.Acronym == @"AP") ? LeaderboardVariant.Autopilot
                : LeaderboardVariant.Vanilla;

            beatmapRankingRequest = new GetScoresRequest(score.BeatmapInfo, score.Ruleset, BeatmapLeaderboardScope.Global, variant: variant);
            beatmapRankingRequest.Success += response => Schedule(() =>
            {
                var own = response.UserScore;

                if (own != null)
                {
                    onlineRank = own.Position;
                    isOnlineBest = own.Score.ID is ulong id && (long)id == score.OnlineID;
                    onlinePp = isOnlineBest ? own.Score.PP : null;
                    if (!isOnlineBest)
                        bestOnlinePp = own.Score.PP;
                }

                beatmapRankingDone = true;
                populateBeatmapServerCells();
            });
            beatmapRankingRequest.Failure += _ => Schedule(() =>
            {
                beatmapRankingDone = true;
                populateBeatmapServerCells();
            });

            api.Queue(beatmapRankingRequest);
        }

        private double? bestOnlinePp;

        /// <summary>
        /// Row 1's server columns: the score's rank and pp on the beatmap's online leaderboard.
        /// </summary>
        private void populateBeatmapServerCells()
        {
            if (!IsLoaded)
                return;

            if (!beatmapRankingDone)
            {
                beatmapRowCells[0].SetContent("Pending...", string.Empty, false);
                beatmapRowCells[5].SetContent("Pending...", string.Empty, false);
                return;
            }

            if (onlineRank is int rank)
            {
                if (isOnlineBest)
                    beatmapRowCells[0].SetContent($"#{rank:N0}", "(new best!)", true);
                else
                    beatmapRowCells[0].SetContent("-", $"your best: #{rank:N0}", false);
            }
            else
                beatmapRowCells[0].SetContent("-", string.Empty, false);

            if (onlinePp is double pp)
                beatmapRowCells[5].SetContent($"{pp:N0}pp", string.Empty, pp > 0);
            else if (bestOnlinePp is double best)
                beatmapRowCells[5].SetContent("-", $"your best: {best:N0}pp", false);
            else
                beatmapRowCells[5].SetContent("-", string.Empty, false);
        }

        protected override void Dispose(bool isDisposing)
        {
            beatmapRankingRequest?.Cancel();
            base.Dispose(isDisposing);
        }

        /// <summary>
        /// Row 2 (Overall Ranking): the player's profile before and after this score, as the server reports it
        /// (<see cref="UserStatisticsWatcher"/>, the same numbers lazer's own results screen shows).
        /// Until that arrives, the current profile is shown without changes.
        /// </summary>
        private void populateOverallRow()
        {
            if (!IsLoaded)
                return;

            var update = statisticsUpdate;

            if (update == null)
            {
                var current = api.LocalUser.Value?.Statistics;

                overallRowCells[0].SetContent(current?.GlobalRank is int r ? $"#{r:N0}" : "-", "...", false);
                overallRowCells[1].SetContent(current != null ? $"{current.Accuracy:0.00}%" : "-", "...", false);
                overallRowCells[2].SetContent(current != null ? $"{current.MaxCombo:N0}x" : "-", "...", false);
                overallRowCells[3].SetContent(current != null ? $"{current.RankedScore:N0}" : "-", "...", false);
                overallRowCells[4].SetContent(current != null ? $"{current.TotalScore:N0}" : "-", "...", false);
                overallRowCells[5].SetContent(current?.PP is decimal p ? $"{p:N0}pp" : "-", "...", false);
                return;
            }

            var before = update.Before;
            var after = update.After;

            if (after.GlobalRank is int afterRank)
            {
                int gained = before.GlobalRank is int beforeRank ? beforeRank - afterRank : 0;
                overallRowCells[0].SetContent($"#{afterRank:N0}", gained > 0 ? $"(+{gained:N0})" : gained < 0 ? $"({gained:N0})" : "0", gained > 0);
            }
            else
                overallRowCells[0].SetContent("-", string.Empty, false);

            double accDiff = after.Accuracy - before.Accuracy;
            overallRowCells[1].SetContent($"{after.Accuracy:0.00}%", formatSigned(accDiff, "0.00", "%"), accDiff > 0.005);

            int comboDiff = after.MaxCombo - before.MaxCombo;
            overallRowCells[2].SetContent($"{after.MaxCombo:N0}x", formatSigned(comboDiff, "N0", "x"), comboDiff > 0);

            long rankedDiff = after.RankedScore - before.RankedScore;
            overallRowCells[3].SetContent($"{after.RankedScore:N0}", formatSigned(rankedDiff, "N0", string.Empty), rankedDiff > 0);

            long totalDiff = after.TotalScore - before.TotalScore;
            overallRowCells[4].SetContent($"{after.TotalScore:N0}", formatSigned(totalDiff, "N0", string.Empty), totalDiff > 0);

            decimal ppDiff = (after.PP ?? 0) - (before.PP ?? 0);
            overallRowCells[5].SetContent($"{after.PP ?? 0:N0}pp", formatSigned((double)ppDiff, "0.##", "pp"), ppDiff > 0);
        }

        private static string formatSigned(double value, string format, string suffix)
        {
            if (Math.Abs(value) < 1e-9)
                return "0";

            return value > 0
                ? $"(+{value.ToString(format)}{suffix})"
                : $"({value.ToString(format)}{suffix})";
        }

        private void addFavourite()
        {
            int? setId = currentScore?.BeatmapInfo?.BeatmapSet?.OnlineID;

            if (setId is not int id || id <= 0)
            {
                addFavouriteRow.SetChecked(false);
                return;
            }

            var request = new PostBeatmapFavouriteRequest(id, BeatmapFavouriteAction.Favourite);
            request.Failure += _ => Schedule(() => addFavouriteRow.SetChecked(false));
            api.Queue(request);
        }

        protected override void PopIn()
        {
            swooshSample?.Play();
            this.MoveToY(0f, PotatoMode.UltraActive ? 0 : 450, Easing.OutQuint).FadeIn(PotatoMode.UltraActive ? 0 : 250, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            this.MoveToY(672f, PotatoMode.UltraActive ? 0 : 450, Easing.OutQuint).FadeOut(PotatoMode.UltraActive ? 0 : 300, Easing.OutQuint);
        }

        private partial class ComparisonCell : CompositeDrawable
        {
            private readonly Color4 baseColour;
            private readonly Box background;
            private readonly OsuSpriteText mainValueText;
            private readonly OsuSpriteText subValueText;

            public ComparisonCell(float width, Color4 baseColour)
            {
                this.baseColour = baseColour;
                Size = new Vector2(width, row_height);

                InternalChildren = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = baseColour,
                    },
                    mainValueText = new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 2f,
                        Font = OsuFont.GetFont(size: 16.5f, weight: FontWeight.Bold),
                        Colour = Color4.White,
                        Shadow = true,
                    },
                    subValueText = new OsuSpriteText
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Y = -2f,
                        Font = OsuFont.GetFont(size: 12.5f, weight: FontWeight.Regular),
                        Colour = Color4.White.Opacity(0.9f),
                        Shadow = true,
                    },
                };
            }

            public void SetContent(string mainText, string subText, bool improved)
            {
                background.Colour = improved ? colour_improved_green : baseColour;
                mainValueText.Font = OsuFont.GetFont(size: 16.5f, weight: improved ? FontWeight.Bold : FontWeight.Regular);
                mainValueText.Text = mainText;
                subValueText.Text = subText;
            }
        }

        private partial class StableOptionRadioRow : OsuClickableContainer
        {
            private readonly Action? onActivated;
            private readonly Box fillBox;
            private bool isChecked;

            public StableOptionRadioRow(string text, Action? onActivated)
            {
                this.onActivated = onActivated;
                AutoSizeAxes = Axes.Both;
                Action = toggle;

                Color4 stablePink = Color4Extensions.FromHex(@"EB459E");

                Child = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8f, 0f),
                    Children = new Drawable[]
                    {
                        new CircularContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(16f, 16f),
                            Masking = true,
                            BorderThickness = 2f,
                            BorderColour = stablePink,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black.Opacity(0.5f),
                                },
                                fillBox = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = stablePink,
                                    Alpha = 0f,
                                },
                            },
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = text,
                            Font = OsuFont.GetFont(size: 13.5f, weight: FontWeight.SemiBold),
                            Colour = Color4.White,
                            Shadow = true,
                        },
                    },
                };
            }

            public void SetChecked(bool value)
            {
                isChecked = value;
                fillBox.Alpha = isChecked ? 1f : 0f;
            }

            private void toggle()
            {
                SetChecked(!isChecked);
                if (isChecked)
                    onActivated?.Invoke();
            }
        }
    }
}
