// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Performance;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterface
{
    /// <summary>
    /// TumThai!Next: osu!stable bottom-left back button using <c>back-button-layer</c> sprites
    /// or custom skin <c>menu-back</c> textures when provided by a user skin.
    /// </summary>
    public partial class StableBackButton : OsuClickableContainer
    {
        public const float HEIGHT = 93f;
        public const float IDLE_WIDTH = 96f;
        public const float HOVER_WIDTH = 138f;

        private static readonly Color4 idle_colour = Color4Extensions.FromHex("EE3399");
        private static readonly Color4 hover_colour = Color4Extensions.FromHex("BB1177");

        private Container defaultLayersContainer = null!;
        private Container customMenuBackContainer = null!;
        private Container customMenuBackAdditiveContainer = null!;
        private Drawable layerLeft = null!;
        private Drawable layerRight = null!;
        private SpriteIcon arrowIcon = null!;
        private OsuSpriteText backText = null!;

        private ISample? hoverSample;
        private ISample? backSample;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        private IBindable<Skin>? currentSkin;

        public StableBackButton()
        {
            Size = new Vector2(HOVER_WIDTH, HEIGHT);
        }

        // osu!stable: a skin's menu-back is clickable over its whole image (drawn at its own size from the bottom-left),
        // not just the default button's box.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            if (IsLoaded && customMenuBackContainer.Alpha > 0 && customMenuBackContainer.Children.Count > 0)
                return customMenuBackContainer.ReceivePositionalInputAt(screenSpacePos);

            return base.ReceivePositionalInputAt(screenSpacePos);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var layerTexture = skinManager.GetClassicTexture(@"back-button-layer");

            Children = new Drawable[]
            {
                defaultLayersContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        // layerRight is drawn behind layerLeft (depth 0.90 in osu!stable)
                        layerRight = layerTexture != null
                            ? new Sprite
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomRight,
                                Texture = layerTexture,
                                X = 96f,
                                Colour = idle_colour,
                            }
                            : new Box
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomRight,
                                Size = new Vector2(160f, HEIGHT),
                                Shear = new Vector2(0.25f, 0),
                                X = 96f,
                                Colour = idle_colour,
                            },
                        // layerLeft is drawn in front of layerRight (depth 0.91 in osu!stable) and darkens on hover
                        layerLeft = layerTexture != null
                            ? new Sprite
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomRight,
                                Texture = layerTexture,
                                X = 35.2f,
                                Colour = idle_colour,
                            }
                            : new Box
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomRight,
                                Size = new Vector2(160f, HEIGHT),
                                Shear = new Vector2(0.25f, 0),
                                X = 35.2f,
                                Colour = idle_colour,
                            },
                        backText = new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.Centre,
                            Text = "back",
                            Font = OsuFont.GetFont(size: 24, weight: FontWeight.Regular),
                            Position = new Vector2(60.5f, -33.6f),
                            Shadow = true,
                            Colour = Color4.White,
                        },
                        arrowIcon = new SpriteIcon
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.Centre,
                            Icon = FontAwesome.Solid.ChevronCircleLeft,
                            Size = new Vector2(28.8f),
                            Position = new Vector2(14.67f, -33.6f),
                            Colour = Color4.White,
                        },
                    },
                },
                customMenuBackContainer = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    AutoSizeAxes = Axes.Both,
                    Alpha = 0,
                },
                customMenuBackAdditiveContainer = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    AutoSizeAxes = Axes.Both,
                    Alpha = 0,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentSkin = skinManager.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ => updateSkin(), true);
        }

        private void updateSkin()
        {
            hoverSample = skinManager.GetClassicSample(@"back-button-hover", @"menuclick") ?? audio.Samples.Get(@"Menu/button-hover");
            backSample = skinManager.GetClassicSample(@"back-button-click", @"menuback") ?? audio.Samples.Get(@"UI/generic-button-back");

            var activeSkin = skinManager.CurrentSkin.Value;
            var customBackAnim = activeSkin is LegacySkin && activeSkin != skinManager.DefaultClassicSkin
                ? activeSkin.GetAnimation(@"menu-back", animatable: true, looping: true, applyConfigFrameRate: true)
                : null;

            customMenuBackContainer.Clear();
            customMenuBackAdditiveContainer.Clear();

            if (customBackAnim != null)
            {
                customBackAnim.Anchor = Anchor.BottomLeft;
                customBackAnim.Origin = Anchor.BottomLeft;
                customMenuBackContainer.Add(customBackAnim);

                var customBackAdditiveAnim = activeSkin.GetAnimation(@"menu-back", animatable: true, looping: true, applyConfigFrameRate: true);
                if (customBackAdditiveAnim != null)
                {
                    customBackAdditiveAnim.Anchor = Anchor.BottomLeft;
                    customBackAdditiveAnim.Origin = Anchor.BottomLeft;
                    customBackAdditiveAnim.Blending = BlendingParameters.Additive;
                    customMenuBackAdditiveContainer.Add(customBackAdditiveAnim);
                }

                customMenuBackContainer.Alpha = 1f;
                defaultLayersContainer.Alpha = 0f;
            }
            else
            {
                customMenuBackContainer.Alpha = 0f;
                customMenuBackAdditiveContainer.Alpha = 0f;
                defaultLayersContainer.Alpha = 1f;

                var layerTex = skinManager.GetClassicTexture(@"back-button-layer");
                if (layerTex != null)
                {
                    if (layerLeft is Sprite sLeft) sLeft.Texture = layerTex;
                    if (layerRight is Sprite sRight) sRight.Texture = layerTex;
                }
            }
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverSample?.Play();

            double duration = PotatoMode.UltraActive ? 0 : 600;
            if (customMenuBackContainer.Alpha > 0)
            {
                customMenuBackAdditiveContainer.FadeTo(0.25f, PotatoMode.UltraActive ? 0 : 150, Easing.OutQuad);
            }
            else if (PotatoMode.UltraActive)
            {
                layerLeft.Colour = hover_colour;
            }
            else
            {
                layerLeft.FadeColour(hover_colour, duration, Easing.OutElastic);
                layerLeft.MoveToX(64f, duration, Easing.OutElastic);
                layerRight.MoveToX(137.6f, duration, Easing.OutElastic);
                arrowIcon.MoveToX(26.67f, duration, Easing.OutElastic);
                backText.MoveToX(94.67f, duration, Easing.OutElastic);
            }

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            double duration = PotatoMode.UltraActive ? 0 : 600;
            if (customMenuBackContainer.Alpha > 0)
            {
                customMenuBackAdditiveContainer.FadeOut(PotatoMode.UltraActive ? 0 : 200, Easing.OutQuad);
            }
            else if (PotatoMode.UltraActive)
            {
                layerLeft.Colour = idle_colour;
            }
            else
            {
                layerLeft.FadeColour(idle_colour, duration, Easing.OutElastic);
                layerLeft.MoveToX(35.2f, duration, Easing.OutElastic);
                layerRight.MoveToX(96f, duration, Easing.OutElastic);
                arrowIcon.MoveToX(14.67f, duration, Easing.OutElastic);
                backText.MoveToX(60.5f, duration, Easing.OutElastic);
            }

            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (Enabled.Value)
                backSample?.Play();

            return base.OnClick(e);
        }
    }
}
