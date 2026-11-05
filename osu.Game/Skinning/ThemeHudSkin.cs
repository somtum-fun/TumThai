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
    /// TumThai: an <see cref="ISkin"/> which supplies a different HUD component set while consulting
    /// the active skin source first and falling back to the HUD source skin for any missing
    /// legacy HUD textures, samples or configuration (such as when the active skin is Argon or Triangles).
    /// </summary>
    public class ThemeHudSkin : ISkin
    {
        private readonly ISkin hudSource;
        private readonly ISkin? userSkinSource;

        public ThemeHudSkin(ISkin hudSource, ISkin? userSkinSource = null)
        {
            this.hudSource = hudSource;
            this.userSkinSource = userSkinSource;
        }

        public Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            if (lookup is GlobalSkinnableContainerLookup global && global.Lookup == GlobalSkinnableContainers.MainHUDComponents)
                return hudSource.GetDrawableComponent(lookup);

            return null;
        }

        public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
            => userSkinSource?.GetTexture(componentName, wrapModeS, wrapModeT)
               ?? hudSource.GetTexture(componentName, wrapModeS, wrapModeT);

        public ISample? GetSample(ISampleInfo sampleInfo)
            => userSkinSource?.GetSample(sampleInfo)
               ?? hudSource.GetSample(sampleInfo);

        public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull
            => userSkinSource?.GetConfig<TLookup, TValue>(lookup)
               ?? hudSource.GetConfig<TLookup, TValue>(lookup);
    }
}
