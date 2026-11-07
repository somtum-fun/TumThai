// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        private readonly Func<ISkin, ISkin?>? rulesetTransformer;
        private readonly ISkin? transformedHudSource;

        /// <param name="hudSource">The skin whose HUD components are used (osu!stable's default skin).</param>
        /// <param name="userSkinSource">The player's skin (or the <see cref="SkinManager"/>), consulted first.</param>
        /// <param name="rulesetTransformer">
        /// The ruleset's skin transformer (Ruleset.CreateSkinTransformer). Ruleset-specific HUD layers (the osu! ruleset's
        /// key counter, for one) only exist on transformed skins, so without it they'd be replaced by a generic layer.
        /// </param>
        public ThemeHudSkin(ISkin hudSource, ISkin? userSkinSource = null, Func<ISkin, ISkin?>? rulesetTransformer = null)
        {
            this.hudSource = hudSource;
            this.userSkinSource = userSkinSource;
            this.rulesetTransformer = rulesetTransformer;
            transformedHudSource = rulesetTransformer?.Invoke(hudSource);
        }

        public Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            // A ruleset's own HUD layer: from the ruleset-transformed skins, as in normal gameplay.
            if (rulesetTransformer != null && transformedHudSource != null)
            {
                if (lookup is UserSkinComponentLookup { Component: GlobalSkinnableContainerLookup { Ruleset: not null } rulesetUser } userRulesetLookup
                    && rulesetUser.Lookup == GlobalSkinnableContainers.MainHUDComponents)
                {
                    var transformedUser = userSkinSource is SkinManager manager && manager.CurrentSkin.Value is LegacySkin legacy
                        ? rulesetTransformer(legacy)
                        : null;

                    return transformedUser?.GetDrawableComponent(userRulesetLookup) ?? transformedHudSource.GetDrawableComponent(rulesetUser);
                }

                if (lookup is GlobalSkinnableContainerLookup { Ruleset: not null } rulesetGlobal && rulesetGlobal.Lookup == GlobalSkinnableContainers.MainHUDComponents)
                    return transformedHudSource.GetDrawableComponent(rulesetGlobal);
            }

            if (lookup is UserSkinComponentLookup userLookup && userLookup.Component is GlobalSkinnableContainerLookup globalUser && globalUser.Lookup == GlobalSkinnableContainers.MainHUDComponents)
            {
                if (userSkinSource is SkinManager sm && sm.CurrentSkin.Value is LegacySkin legacyUserSkin)
                    return legacyUserSkin.GetDrawableComponent(lookup) ?? hudSource.GetDrawableComponent(globalUser);

                return hudSource.GetDrawableComponent(globalUser);
            }

            if (lookup is GlobalSkinnableContainerLookup global && global.Lookup == GlobalSkinnableContainers.MainHUDComponents)
                return hudSource.GetDrawableComponent(lookup);

            return null;
        }

        public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
            => (userSkinSource is SkinManager sm ? sm.CurrentSkin.Value?.GetTexture(componentName, wrapModeS, wrapModeT) : userSkinSource?.GetTexture(componentName, wrapModeS, wrapModeT))
               ?? hudSource.GetTexture(componentName, wrapModeS, wrapModeT);

        public ISample? GetSample(ISampleInfo sampleInfo)
            => (userSkinSource is SkinManager sm
                   ? (sm.CurrentSkin.Value is LegacySkin legacy ? legacy.GetSample(sampleInfo) : null)
                   : userSkinSource?.GetSample(sampleInfo))
               ?? hudSource.GetSample(sampleInfo);

        public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull
            => (userSkinSource is SkinManager sm
                   ? (sm.CurrentSkin.Value is LegacySkin legacy ? legacy.GetConfig<TLookup, TValue>(lookup) : null)
                   : userSkinSource?.GetConfig<TLookup, TValue>(lookup))
               ?? hudSource.GetConfig<TLookup, TValue>(lookup);
    }
}
