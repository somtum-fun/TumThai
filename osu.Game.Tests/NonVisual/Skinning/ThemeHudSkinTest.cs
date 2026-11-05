// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.OpenGL.Textures;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class ThemeHudSkinTest
    {
        [Test]
        public void TestMainHudLookupUsesHudSource()
        {
            var hudDrawable = new Container();
            var userDrawable = new Container();
            var hudSource = new StubSkin { MainHud = hudDrawable };
            var userSource = new StubSkin { MainHud = userDrawable };

            var themeSkin = new ThemeHudSkin(hudSource, userSource);

            Assert.That(themeSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents)), Is.SameAs(hudDrawable));
            Assert.That(themeSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect)), Is.Null);
        }

        [Test]
        public void TestTextureAndConfigPreferUserSkinAndFallbackToHudSource()
        {
            var renderer = new DummyRenderer();
            var hudScoreTex = renderer.CreateTexture(4, 4);
            var hudBarTex = renderer.CreateTexture(8, 8);
            var userScoreTex = renderer.CreateTexture(16, 16);

            var hudSource = new StubSkin
            {
                ScoreTexture = hudScoreTex,
                HealthBarTexture = hudBarTex,
                ScorePrefix = "score-classic",
            };

            var userSource = new StubSkin
            {
                ScoreTexture = userScoreTex,
                HealthBarTexture = null,
                ScorePrefix = null,
            };

            var themeSkin = new ThemeHudSkin(hudSource, userSource);

            // User skin overrides score-0 when present, but falls back to hudSource for scorebar-bg (e.g. on Argon/Triangles).
            Assert.That(themeSkin.GetTexture("score-0", WrapMode.None, WrapMode.None), Is.SameAs(userScoreTex));
            Assert.That(themeSkin.GetTexture("scorebar-bg", WrapMode.None, WrapMode.None), Is.SameAs(hudBarTex));
            Assert.That(themeSkin.GetConfig<SkinConfiguration.LegacySetting, string>(SkinConfiguration.LegacySetting.ScorePrefix)?.Value, Is.EqualTo("score-classic"));
        }

        private sealed class StubSkin : ISkin
        {
            public Drawable? MainHud { get; init; }
            public Texture? ScoreTexture { get; init; }
            public Texture? HealthBarTexture { get; init; }
            public string? ScorePrefix { get; init; }

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
            {
                if (lookup is GlobalSkinnableContainerLookup global && global.Lookup == GlobalSkinnableContainers.MainHUDComponents)
                    return MainHud;

                return null;
            }

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => componentName switch
            {
                "score-0" => ScoreTexture,
                "scorebar-bg" => HealthBarTexture,
                _ => null,
            };

            public ISample? GetSample(ISampleInfo sampleInfo) => null;

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
            {
                if (lookup is SkinConfiguration.LegacySetting.ScorePrefix && ScorePrefix is TValue val)
                    return new Bindable<TValue>(val);

                return null;
            }
        }
    }
}
