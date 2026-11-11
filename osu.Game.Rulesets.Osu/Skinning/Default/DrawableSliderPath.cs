// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Lines;
using osu.Game.Performance;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    public abstract partial class DrawableSliderPath : SmoothPath, IBufferedDrawable
    {
        Vector2 IBufferedDrawable.FrameBufferScale => PotatoMode.UltraActive ? new Vector2(0.5f) : Vector2.One;

        public const float BORDER_PORTION = 0.128f;
        public const float GRADIENT_PORTION = 1 - BORDER_PORTION;

        private const float border_max_size = 8f;
        private const float border_min_size = 0f;

        private Color4 borderColour = Color4.White;

        public Color4 BorderColour
        {
            get => borderColour;
            set
            {
                if (borderColour == value)
                    return;

                borderColour = value;

                InvalidateTexture();
            }
        }

        private Color4 accentColour = Color4.White;

        public Color4 AccentColour
        {
            get => accentColour;
            set
            {
                if (accentColour == value)
                    return;

                accentColour = value;

                InvalidateTexture();
            }
        }

        private float borderSize = 1;

        public float BorderSize
        {
            get => borderSize;
            set
            {
                if (borderSize == value)
                    return;

                if (value < border_min_size || value > border_max_size)
                    return;

                borderSize = value;

                InvalidateTexture();
            }
        }

        protected float CalculatedBorderPortion => BorderSize * BORDER_PORTION;
    }
}
