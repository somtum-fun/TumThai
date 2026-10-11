// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Localisation;
using osuTK;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// TumThai: osu!lazer's old startup disclaimer (removed upstream in 2023), brought back to say what
    /// TumThai is and that it isn't osu!. Shown before the intro; a click or key skips it.
    /// </summary>
    public partial class Disclaimer : StartupScreen
    {
        public const string GITHUB_URL = @"https://github.com/somtum-fun/TumThai";

        private Sprite logo;
        private LinkFlowContainer textFlow;
        private FillFlowContainer fill;

        private const float logo_y = -110;
        private const float logo_size = 60;

        private readonly OsuScreen nextScreen;

        private bool exiting;

        public Disclaimer(OsuScreen nextScreen = null)
        {
            this.nextScreen = nextScreen;
            ValidForResume = false;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, TextureStore textures)
        {
            // The logo's green (OsuLogo's).
            var tumThaiGreen = Color4Extensions.FromHex(@"86b06c");

            InternalChildren = new Drawable[]
            {
                logo = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = textures.Get(@"Icons/Logo"),
                    Size = new Vector2(logo_size),
                    Y = logo_y,
                },
                fill = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Y = logo_y + 40,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.TopCentre,
                    Children = new Drawable[]
                    {
                        textFlow = new LinkFlowContainer
                        {
                            Width = 680,
                            AutoSizeAxes = Axes.Y,
                            TextAnchor = Anchor.TopCentre,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Spacing = new Vector2(0, 2),
                        },
                    }
                },
            };

            static void formatRegular(SpriteText t) => t.Font = OsuFont.GetFont(size: 20, weight: FontWeight.Regular);
            static void formatBold(SpriteText t) => t.Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold);

            textFlow.AddText(DisclaimerStrings.TitlePart, t => t.Font = t.Font.With(Typeface.Torus, 30, FontWeight.Regular));
            textFlow.AddText(@"TumThai!", t =>
            {
                t.Font = t.Font.With(Typeface.Torus, 30, FontWeight.Regular);
                t.Colour = tumThaiGreen;
            });

            textFlow.NewParagraph();

            textFlow.AddText(DisclaimerStrings.BasedOnParagraph, formatRegular);
            textFlow.AddText(@"osu!lazer", t =>
            {
                t.Font = t.Font.With(Typeface.Torus, 20, FontWeight.Bold);
                t.Colour = colours.Pink;
            });
            textFlow.AddText(DisclaimerStrings.OriginalCodeParagraph, formatRegular);

            textFlow.NewParagraph();
            textFlow.NewParagraph();

            textFlow.AddText(DisclaimerStrings.CommunityProjectParagraph, formatBold);

            textFlow.NewParagraph();
            textFlow.NewParagraph();

            textFlow.AddText(DisclaimerStrings.ReportGitHubParagraph, formatRegular);
            textFlow.NewLine();
            textFlow.AddLink(GITHUB_URL, GITHUB_URL, t =>
            {
                t.Font = t.Font.With(Typeface.Torus, 20, FontWeight.Bold);
                t.Colour = colours.Blue;
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (nextScreen != null)
                LoadComponentAsync(nextScreen);
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            logo.RotateTo(10);
            logo.FadeOut();
            logo.ScaleTo(0.5f);

            logo.Delay(500).FadeIn(500).ScaleTo(1, 500, Easing.OutQuint);

            using (BeginDelayedSequence(2000))
            {
                logo.MoveToY(logo_y * 1.2f, 500, Easing.OutCirc)
                    .RotateTo(-360, 520, Easing.OutQuint)
                    .Then()
                    .MoveToY(logo_y, 160, Easing.InQuart);

                using (BeginDelayedSequence(520 + 160))
                    fill.MoveToOffset(new Vector2(0, 15), 160, Easing.OutQuart);
            }

            double delay = 500;
            foreach (var c in textFlow.Children)
                c.FadeTo(0.001f).Delay(delay += 15).FadeIn(500);

            this.FadeInFromZero(500);
            Scheduler.AddDelayed(next, 4500);
        }

        // A click on the GitHub link opens it instead (the link handles the click first).
        protected override bool OnClick(ClickEvent e)
        {
            next();
            return true;
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat)
                return false;

            next();
            return true;
        }

        private void next()
        {
            if (exiting || nextScreen == null)
                return;

            // The intro is still loading (a slow first start): try again shortly.
            if (nextScreen.LoadState < LoadState.Ready)
            {
                Scheduler.AddDelayed(next, 100);
                return;
            }

            exiting = true;

            this.FadeOut(250)
                .ScaleTo(0.9f, 250, Easing.InQuint)
                .Finally(_ => this.Push(nextScreen));
        }
    }
}
