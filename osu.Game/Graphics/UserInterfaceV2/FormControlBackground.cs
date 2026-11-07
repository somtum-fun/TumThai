// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterfaceV2
{
    public partial class FormControlBackground : CompositeDrawable
    {
        public const float CORNER_EXPONENT = 2.5f;
        public const float BORDER_THICKNESS = 2.5f;

        private VisualStyle visualStyle;
        private IBindable<InterfaceTheme>? interfaceTheme;

        public VisualStyle VisualStyle
        {
            get => visualStyle;
            set
            {
                visualStyle = value;
                updateStyle();
            }
        }

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly Box box;
        private readonly Box flashLayer;

        private readonly HoverSounds sounds;

        public FormControlBackground()
        {
            RelativeSizeAxes = Axes.Both;

            Masking = true;
            CornerRadius = 5;
            CornerExponent = 2.5f;

            CornerExponent = CORNER_EXPONENT;
            BorderThickness = BORDER_THICKNESS;

            InternalChildren = new Drawable[]
            {
                box = new Box
                {
                    Colour = Color4.White,
                    RelativeSizeAxes = Axes.Both,
                },
                flashLayer = new Box
                {
                    Colour = Colour4.Transparent,
                    RelativeSizeAxes = Axes.Both,
                },
                sounds = new HoverSounds(),
            };
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager? config)
        {
            interfaceTheme = config?.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            interfaceTheme?.BindValueChanged(_ => updateStyle());
            updateStyle();
            FinishTransforms(true);
        }

        private void flash(Colour4 flashColour, double duration)
        {
            flashLayer.Colour = ColourInfo.GradientVertical(flashColour.Opacity(0), flashColour);
            flashLayer.FadeOutFromOne(duration, Easing.OutQuint);
        }

        /// <summary>
        /// Use when indicating that a change in value or a definitive action has occurred.
        /// </summary>
        public void FlashOnCommit() => flash(colourProvider.Dark2, 800);

        /// <summary>
        /// Use when rejecting the user's input as incorrect.
        /// </summary>
        public void FlashOnInputError() => flash(Colour4.Red, 200);

        private void updateStyle()
        {
            sounds.Enabled.Value = visualStyle != VisualStyle.Disabled;

            bool isThemed = interfaceTheme?.Value.IsThemed() == true;
            if (isThemed)
            {
                CornerRadius = 3;
                BorderThickness = 1.5f;
                Color4 stableBg = Color4.Black.Opacity(0.55f);
                Color4 stableBorder = visualStyle == VisualStyle.Hovered || visualStyle == VisualStyle.Focused
                    ? Color4Extensions.FromHex(@"EB459E")
                    : Color4.White.Opacity(0.22f);

                this.TransformTo(nameof(BorderColour), (ColourInfo)stableBorder, 150, Easing.OutQuint);
                box.FadeColour(stableBg, 150, Easing.OutQuint);
                return;
            }

            CornerRadius = 5;
            BorderThickness = BORDER_THICKNESS;

            ColourInfo colour;
            ColourInfo borderColour;

            bool border = false;

            switch (visualStyle)
            {
                case VisualStyle.Normal:
                    colour = colourProvider.Background4.Darken(0.1f);
                    borderColour = colourProvider.Light4;
                    break;

                case VisualStyle.Disabled:
                    colour = colourProvider.Background4;
                    borderColour = colourProvider.Dark1;
                    break;

                case VisualStyle.Hovered:
                    colour = ColourInfo.GradientVertical(colourProvider.Background5, colourProvider.Dark4);
                    borderColour = colourProvider.Light4;
                    border = true;
                    break;

                case VisualStyle.Focused:
                    colour = ColourInfo.GradientVertical(colourProvider.Background5, colourProvider.Dark3);
                    border = true;
                    borderColour = colourProvider.Highlight1;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            this.TransformTo(nameof(BorderColour), border ? borderColour : colour, 250, Easing.OutQuint);

            box.FadeColour(colour, 250, Easing.OutQuint);
        }
    }

    public enum VisualStyle
    {
        Normal,
        Disabled,
        Hovered,
        Focused
    }
}
