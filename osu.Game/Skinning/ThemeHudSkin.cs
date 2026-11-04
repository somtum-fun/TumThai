// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;

namespace osu.Game.Skinning
{
    /// <summary>
    /// TumThai: an <see cref="ISkin"/> which only supplies a different HUD component set,
    /// leaving every other lookup (textures, samples, playfield, user layouts) to the normal skin sources.
    /// </summary>
    public class ThemeHudSkin : ISkin
    {
        private readonly ISkin hudSource;

        public ThemeHudSkin(ISkin hudSource)
        {
            this.hudSource = hudSource;
        }

        public Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            if (lookup is GlobalSkinnableContainerLookup global && global.Lookup == GlobalSkinnableContainers.MainHUDComponents)
                return hudSource.GetDrawableComponent(lookup);

            return null;
        }

        public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

        public ISample? GetSample(ISampleInfo sampleInfo) => null;

        public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull
            => null;
    }
}
