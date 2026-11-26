// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
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

        /// <summary>TumThai: the Rev 11 results colours for the grade (pink SS).</summary>
        public bool Rev11Colours { get; init; }

        /// <summary>Invoked once the intro is gone (played out or skipped).</summary>
        public Action? Finished;

        /// <summary>Invoked as the intro starts to step aside: the moment to bring the results in.</summary>
        public Action? Revealing;

        private bool revealed;

        private void reveal()
        {
            if (revealed) return;

            revealed = true;
            Revealing?.Invoke();
        }

        private Container comboBanner = null!;
        private FillFlowContainer comboLetters = null!;
        private Drawable rankText = null!;
        private Container rays = null!;
        private Drawable[] stars = null!;
        private Sample? rankSample;
        private Sample? swooshSample;
        private Sample? tickSample;
        private Sample? starSample;
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
                        Children = new[]
                        {
                            star(),
                            comboLetters = new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                // starts spread out and closes in (the storyboard's first frames)
                                Spacing = new Vector2(70, 0),
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
                        Alpha = 0,
                    }
                    : new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = DrawableRank.GetRankLetter(score.Rank),
                        Font = OsuFont.Torus.With(size: 260, weight: FontWeight.Bold),
                        Alpha = 0,
                    },
                rays = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Alpha = 0,
                },
            };

            // The burst around the grade: eight short rays, pointing out.
            for (int i = 0; i < 8; i++)
            {
                rays.Add(new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Rotation = i * 45,
                    Child = new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.BottomCentre,
                        Size = new Vector2(10, 46),
                        Y = -200,
                    },
                });
            }

            stars = ((FillFlowContainer)comboBanner.Child).Children.Where(c => c is SpriteIcon).ToArray();
            foreach (var s in stars)
                s.Alpha = 0;

            foreach (char c in comboWord)
            {
                comboLetters.Add(createLetter(c, comboLetters.Count));
            }

            rankSample = audio.Samples.Get(rankImpactSample(score.Rank));
            swooshSample = audio.Samples.Get(@"Results/swoosh-up");
            tickSample = audio.Samples.Get(@"Results/score-tick");
            starSample = audio.Samples.Get(@"Results/badge-dink-max");
        }

        private static readonly Color4[] stroke_colours =
        {
            Color4Extensions.FromHex(@"ff4f9a"), Color4Extensions.FromHex(@"7a4dff"), Color4Extensions.FromHex(@"40b7ff"),
            Color4Extensions.FromHex(@"44dd66"), Color4Extensions.FromHex(@"ffc845"), Color4Extensions.FromHex(@"ff5a4a"),
        };

        /// <summary>A letter of the banner: the white letter (first child, hidden at first) and the coloured strokes it's
        /// drawn from, as the Rev 11 storyboard opens with.</summary>
        private Drawable createLetter(char c, int index)
        {
            var letter = new Container<Drawable>
            {
                AutoSizeAxes = Axes.Both,
                Alpha = 0,
                Child = new OsuSpriteText
                {
                    Text = c.ToString(),
                    Font = OsuFont.Torus.With(size: 64, weight: FontWeight.Bold),
                    Alpha = 0,
                },
            };

            if (c == ' ')
                return letter;

            var colour = stroke_colours[index % stroke_colours.Length];
            float[] angles = { 0, 0, -35, 90, 35 };

            // one main stroke and, for most letters, a shorter second one
            letter.Add(stroke(new Vector2(0, -2), new Vector2(11, 46), angles[index % angles.Length], colour));
            if (index % 3 != 2)
                letter.Add(stroke(new Vector2(index % 2 == 0 ? 10 : -10, 12), new Vector2(11, 22), angles[(index + 2) % angles.Length], colour));

            return letter;
        }

        private static Drawable stroke(Vector2 offset, Vector2 size, float rotation, Color4 colour) => new Circle
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Position = offset,
            Size = size,
            Rotation = rotation,
            Colour = ColourInfo.GradientVertical(Color4.White, colour),
        };

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
                comboLetters.TransformSpacingTo(new Vector2(26, 0), 800, Easing.OutQuint);

                // sounds: a swoosh as it opens, a tick rising in pitch per letter, a ding as the stars turn in
                swooshSample?.Play();

                for (int n = 0; n < comboLetters.Count; n++)
                {
                    double pitch = 1 + n * 0.05;
                    Scheduler.AddDelayed(() => playAt(tickSample, pitch), 150 + n * letter_stagger);
                }

                Scheduler.AddDelayed(() => starSample?.Play(), 450);

                foreach (var s in stars)
                    s.Delay(450).ScaleTo(0.4f).FadeIn(200).ScaleTo(1, 500, Easing.OutBack).RotateTo(-20).RotateTo(0, 600, Easing.OutQuint);

                int i = 0;

                foreach (var letter in comboLetters.Children)
                {
                    double at = 150 + i++ * letter_stagger;

                    // the storyboard: coloured strokes first, which then become the white letter
                    letter.Delay(at).MoveToY(30).FadeIn(150, Easing.OutQuint).MoveToY(0, 400, Easing.OutBack);

                    if (letter is Container<Drawable> parts)
                    {
                        foreach (var piece in parts.Children.Skip(1))
                            piece.Delay(at).ScaleTo(new Vector2(1, 0.2f)).ScaleTo(1, 250, Easing.OutQuint).Then().Delay(250).FadeOut(250, Easing.OutQuint);

                        parts.Children[0].Delay(at + 350).FadeIn(250, Easing.OutQuint);
                    }
                }

                comboBanner.Delay(combo_hold).ScaleTo(1.05f, 300, Easing.OutQuint).FadeOut(300, Easing.OutQuint);
                rankAt = combo_hold + 300;
            }

            using (BeginDelayedSequence(rankAt))
            {
                Schedule(() => rankSample?.Play());
                // white first, with the burst; then the grade's own colour (the storyboard's middle frames)
                rankText.Colour = Color4.White;
                rankText.ScaleTo(2.2f).FadeIn(120).ScaleTo(1, 450, Easing.OutQuint);
                rankText.Delay(450).FadeColour(ColourInfo.GradientVertical(Color4.White, Rev11Colours && V2RankEmblem.Rev11Accent(score.Rank) is Colour4 pink ? pink : OsuColour.ForRank(score.Rank)), 400, Easing.OutQuint);

                rays.FadeIn(80).Then().Delay(250).FadeOut(350);
                foreach (var ray in rays.Children.OfType<Container>())
                    ray.Child.MoveToY(-170).MoveToY(-260, 600, Easing.OutQuint);

                // the grade settles back as the results come in
                rankText.Delay(rank_hold - 150).ScaleTo(0.6f, 450, Easing.InQuint);
            }

            this.Delay(rankAt + rank_hold).Schedule(reveal).FadeOut(350, Easing.OutQuint).OnComplete(_ => finish());
        }

        protected override bool OnClick(ClickEvent e)
        {
            reveal();
            this.FadeOut(150).OnComplete(_ => finish());
            return true;
        }

        private void finish()
        {
            if (done) return;

            done = true;
            reveal();
            Finished?.Invoke();
            Expire();
        }

        private static void playAt(Sample? sample, double pitch)
        {
            var channel = sample?.GetChannel();
            if (channel == null) return;

            channel.Frequency.Value = pitch;
            channel.Play();
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
