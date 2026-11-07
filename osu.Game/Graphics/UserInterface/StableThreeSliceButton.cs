// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterface
{
    /// <summary>
    /// TumThai!Next: osu!stable 3-slice glossy button using <c>button-left</c>, <c>button-middle</c>, <c>button-right</c>.
    /// </summary>
    public partial class StableThreeSliceButton : OsuClickableContainer
    {
        private Color4 backgroundColour = Color4Extensions.FromHex("E96800");

        public Color4 BackgroundColour
        {
            get => backgroundColour;
            set
            {
                backgroundColour = value;
                if (IsLoaded)
                    sliceContainer.Colour = value;
            }
        }

        private LocalisableString text;

        public LocalisableString Text
        {
            get => text;
            set
            {
                text = value;
                if (IsLoaded)
                    spriteText.Text = value;
            }
        }

        public float FontSize { get; init; } = 24f;
        public float HoverScale { get; init; } = 1.05f;
        public string ClickSampleName { get; init; } = @"menuhit";

        private Container content = null!;
        private Container sliceContainer = null!;
        private Box hoverFlash = null!;
        private OsuSpriteText spriteText = null!;

        private ISample? hoverSample;
        private ISample? clickSample;

        [BackgroundDependencyLoader]
        private void load(SkinManager skinManager, AudioManager audio)
        {
            hoverSample = skinManager.GetClassicSample(@"menuclick") ?? audio.Samples.Get(@"Menu/button-hover");
            clickSample = skinManager.GetClassicSample(ClickSampleName) ?? audio.Samples.Get(@"UI/button-select");

            var leftTex = skinManager.GetClassicTexture(@"button-left");
            var midTex = skinManager.GetClassicTexture(@"button-middle");
            var rightTex = skinManager.GetClassicTexture(@"button-right");

            Child = content = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    sliceContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = backgroundColour,
                        Children = leftTex != null && midTex != null && rightTex != null
                            ? new Drawable[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(GridSizeMode.AutoSize),
                                        new Dimension(),
                                        new Dimension(GridSizeMode.AutoSize),
                                    },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            new Sprite
                                            {
                                                Texture = leftTex,
                                                RelativeSizeAxes = Axes.Y,
                                                Height = 1f,
                                                FillMode = FillMode.Fit,
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                            },
                                            new Sprite
                                            {
                                                Texture = midTex,
                                                RelativeSizeAxes = Axes.Both,
                                                Size = Vector2.One,
                                            },
                                            new Sprite
                                            {
                                                Texture = rightTex,
                                                RelativeSizeAxes = Axes.Y,
                                                Height = 1f,
                                                FillMode = FillMode.Fit,
                                                Anchor = Anchor.CentreRight,
                                                Origin = Anchor.CentreRight,
                                            },
                                        }
                                    }
                                }
                            }
                            : new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Masking = true,
                                    CornerRadius = 8f,
                                    Child = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.White,
                                    }
                                }
                            }
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 8f,
                        Child = hoverFlash = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.White,
                            Blending = BlendingParameters.Additive,
                            Alpha = 0f,
                        }
                    },
                    spriteText = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = text,
                        Font = OsuFont.GetFont(size: FontSize, weight: FontWeight.SemiBold),
                        Shadow = true,
                        Colour = Color4.White,
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverSample?.Play();
            content.ScaleTo(HoverScale, 150, Easing.OutQuad);
            hoverFlash.FadeTo(0.18f, 120, Easing.OutQuad);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            content.ScaleTo(1f, 150, Easing.OutQuad);
            hoverFlash.FadeOut(150, Easing.OutQuad);
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (Enabled.Value)
            {
                clickSample?.Play();
                hoverFlash.FadeTo(0.6f).Then().FadeTo(IsHovered ? 0.18f : 0f, 350, Easing.OutQuad);
            }

            return base.OnClick(e);
        }
    }
}
