// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Leaderboards;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// TumThai: the short show before the V2 results of a play just finished: "FULL COMBO!" (or "PERFECT!") when it
    /// was one, then the grade, big, then out of the way. A click skips it.
    /// </summary>
    public partial class V2ResultsIntro : CompositeDrawable
    {
        private const double letter_stagger = 45;
        private const double combo_hold = 1100;
        private const double rank_hold = 1000;

        private readonly ScoreInfo score;

        /// <summary>Invoked once the intro is gone (played out or skipped).</summary>
        public Action? Finished;

        private Container comboBanner = null!;
        private FillFlowContainer comboLetters = null!;
        private Drawable rankText = null!;
        private Sample? rankSample;
        private bool done;

        public V2ResultsIntro(ScoreInfo score)
        {
            this.score = score;
            RelativeSizeAxes = Axes.Both;
        }

        private bool isPerfect => score.IsPerfectCombo();
        private bool isFullCombo => isPerfect || (score.Passed && score.GetMaximumAchievableCombo() > 0 && score.MaxCombo == score.GetMaximumAchievableCombo());

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, TextureStore textures)
        {
            // The grade as a picture when there is one (sharp at any size; a font this big is stretched and blurs).
            var rankTexture = textures.Get($"V2/Ranks/rank-{DrawableRank.GetRankLetter(score.Rank)}");

            string comboWord = isPerfect ? "PERFECT!" : "FULL COMBO!";

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.85f,
                },
                comboBanner = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Alpha = 0,
                    Child = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(36, 0),
                        Children = new Drawable[]
                        {
                            star(),
                            comboLetters = new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(26, 0),
                            },
                            star(),
                        },
                    },
                },
                rankText = rankTexture != null
                    ? new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Texture = rankTexture,
                        Size = new Vector2(480, 360),
                        Colour = DrawableRank.GetRankLetterColour(score.Rank),
                        Alpha = 0,
                    }
                    : new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = DrawableRank.GetRankLetter(score.Rank),
                        Font = OsuFont.Torus.With(size: 260, weight: FontWeight.Bold),
                        Colour = DrawableRank.GetRankLetterColour(score.Rank),
                        Alpha = 0,
                    },
            };

            foreach (char c in comboWord)
            {
                comboLetters.Add(new OsuSpriteText
                {
                    Text = c.ToString(),
                    Font = OsuFont.Torus.With(size: 64, weight: FontWeight.Bold),
                    Alpha = 0,
                });
            }

            rankSample = audio.Samples.Get(rankImpactSample(score.Rank));
        }

        private Drawable star() => new SpriteIcon
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Icon = FontAwesome.Regular.Star,
            Size = new Vector2(56),
            Colour = ColourInfo.GradientHorizontal(Color4Extensions.FromHex(@"66ffcc"), Color4Extensions.FromHex(@"ff99cc")),
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            this.FadeInFromZero(200);

            double rankAt = 250;

            if (isFullCombo)
            {
                comboBanner.FadeIn(150);

                int i = 0;

                foreach (var letter in comboLetters.Children)
                {
                    letter.Delay(150 + i++ * letter_stagger)
                          .MoveToY(30).FadeIn(200, Easing.OutQuint).MoveToY(0, 400, Easing.OutBack);
                }

                comboBanner.Delay(combo_hold).ScaleTo(1.05f, 300, Easing.OutQuint).FadeOut(300, Easing.OutQuint);
                rankAt = combo_hold + 300;
            }

            using (BeginDelayedSequence(rankAt))
            {
                Schedule(() => rankSample?.Play());
                rankText.ScaleTo(2.2f).FadeIn(120).ScaleTo(1, 450, Easing.OutQuint);
            }

            this.Delay(rankAt + rank_hold).FadeOut(350, Easing.OutQuint).OnComplete(_ => finish());
        }

        protected override bool OnClick(ClickEvent e)
        {
            this.FadeOut(150).OnComplete(_ => finish());
            return true;
        }

        private void finish()
        {
            if (done) return;

            done = true;
            Finished?.Invoke();
            Expire();
        }

        private static string rankImpactSample(ScoreRank rank)
        {
            switch (rank)
            {
                case ScoreRank.D:
                    return @"Results/rank-impact-fail-d";

                case ScoreRank.C:
                case ScoreRank.B:
                    return @"Results/rank-impact-fail";

                case ScoreRank.X:
                case ScoreRank.XH:
                    return @"Results/rank-impact-pass-ss";

                default:
                    return @"Results/rank-impact-pass";
            }
        }
    }
}
