// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// osu!stable Song Select bottom bar containing <c>songselect-bottom</c>, <see cref="StableBackButton"/>,
    /// the 4 classic sprite buttons (Mode, Mods, Random, Beatmap Options) at native <c>@2x</c> sizes,
    /// <see cref="StableUserPanel"/>, the <c>menu-osu</c> bottom-right start button, and the popup Mode Menu.
    /// </summary>
    public partial class StableSongSelectFooter : VisibilityContainer
    {
        public const float HEIGHT = 101f;

        private readonly ModSelectOverlay modSelectOverlay;
        private readonly Action nextRandom;
        private readonly Action previousRandom;
        private readonly Action onStart;
        private readonly Action onBack;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private IBindable<Skin> currentSkin = null!;
        private IBindable<InterfaceTheme> interfaceTheme = null!;

        private Drawable bottomBarDrawable = null!;
        private Sprite modeSmallSprite = null!;
        private Sprite modeWatermarkSprite = null!;
        private Container modeMenuOverlay = null!;
        private StableSelectionSpriteButton modeButton = null!;
        private StableSelectionSpriteButton modsButton = null!;
        private StableSelectionSpriteButton randomButton = null!;
        private OptionsSpriteButton optionsButton = null!;
        private StableStartOsuCookie startCookie = null!;

        public StableSongSelectFooter(ModSelectOverlay modSelectOverlay, Action nextRandom, Action previousRandom, Action onStart, Action onBack)
        {
            this.modSelectOverlay = modSelectOverlay;
            this.nextRandom = nextRandom;
            this.previousRandom = previousRandom;
            this.onStart = onStart;
            this.onBack = onBack;

            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            interfaceTheme = config.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);
            var bottomTex = skinManager.GetClassicTexture(@"songselect-bottom");

            InternalChildren = new Drawable[]
            {
                // songselect-bottom bar stretched horizontally across the bottom (height 101 at @2x)
                bottomBarDrawable = bottomTex != null
                    ? new Sprite
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Width = 1f,
                        Height = HEIGHT,
                        Texture = bottomTex,
                    }
                    : new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 90f,
                        Colour = Color4.Black.Opacity(0.92f),
                    },
                // Center background mode watermark clipped inside the bottom bar so it never sticks out above the blue line
                new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 88f,
                    Masking = true,
                    Child = modeWatermarkSprite = new Sprite
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.Centre,
                        Position = new Vector2(0f, -10f),
                        Scale = new Vector2(0.45f),
                        Alpha = 0.16f,
                        Blending = BlendingParameters.Additive,
                    },
                },
                // Bottom-left osu!stable back button
                new StableBackButton
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Action = onBack,
                },
                // 4 classic selection buttons placed at osu!stable's exact 768p X coordinates (SongSelection.cs lines 1335-1454)
                modeButton = new StableSelectionSpriteButton(
                    @"selection-mode",
                    @"selection-mode-over",
                    @"Mode",
                    toggleModeMenu)
                {
                    X = 224f,
                    ExtraOverlay = modeSmallSprite = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Position = new Vector2(0f, -10f),
                        Alpha = 0.65f,
                        Blending = BlendingParameters.Additive,
                    },
                },
                modsButton = new StableSelectionSpriteButton(
                    @"selection-mods",
                    @"selection-mods-over",
                    @"Mods",
                    () => modSelectOverlay.ToggleVisibility())
                {
                    X = 316.16f,
                },
                randomButton = new StableSelectionSpriteButton(
                    @"selection-random",
                    @"selection-random-over",
                    @"Random",
                    nextRandom,
                    previousRandom)
                {
                    X = 392.96f,
                },
                optionsButton = new OptionsSpriteButton(
                    @"selection-options",
                    @"selection-options-over",
                    colourProvider,
                    songSelect,
                    workingBeatmap,
                    realm)
                {
                    X = 469.76f,
                },
                // User profile panel at X = 623.36f (389.6 * 1.6 in SongSelection.cs lines 1452-1454)
                new StableUserPanel
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    X = 623.36f,
                },
                // Bottom-right pulsing start button at (680, 510) -> (-64, -48) from BottomRight.
                // TumThai: the somtum.fun logo, not osu!'s cookie (a player's own skin can still bring its menu-osu).
                startCookie = new StableStartOsuCookie(onStart)
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-64f, -48f),
                },
                // Popup Mode Selection Menu (SongSelection.cs modeMenu)
                createModeMenuOverlay(),
            };
        }

        private Container createModeMenuOverlay()
        {
            var modeRows = new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
            };

            var entries = new (int Index, string ShortName, string Label, string MedTex)[]
            {
                (1, @"osu", @"osu!", @"mode-osu-med"),
                (2, @"taiko", @"osu!taiko", @"mode-taiko-med"),
                (3, @"fruits", @"osu!catch", @"mode-fruits-med"),
                (4, @"mania", @"osu!mania", @"mode-mania-med"),
            };

            foreach (var entry in entries)
            {
                modeRows.Add(new ModeMenuOptionRow(
                    entry.Index,
                    entry.Label,
                    skinManager.DefaultClassicSkin.GetTexture(entry.MedTex),
                    () => selectRulesetByShortName(entry.ShortName)));
            }

            return modeMenuOverlay = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0f,
                Children = new Drawable[]
                {
                    new ClickableContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Action = () => modeMenuOverlay.FadeOut(150, Easing.OutQuint),
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.65f),
                        },
                    },
                    modeRows,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentSkin = skinManager.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ => updateSkin());

            interfaceTheme.BindValueChanged(t =>
            {
                bool isDroid = t.NewValue.Effective() == InterfaceTheme.OsuDroid;
                bottomBarDrawable.Alpha = isDroid ? 0f : 1f;
            }, true);

            ruleset.BindValueChanged(_ => updateModeSprites(), true);
        }

        private void updateSkin()
        {
            if (bottomBarDrawable is Sprite s)
                s.Texture = skinManager.GetClassicTexture(@"songselect-bottom");

            modeButton.UpdateSkin(skinManager);
            modsButton.UpdateSkin(skinManager);
            randomButton.UpdateSkin(skinManager);
            optionsButton.UpdateSkin(skinManager);
            startCookie.UpdateSkin(skinManager);
            updateModeSprites();
        }

        private void updateModeSprites()
        {
            var (smallTex, bigTex) = ruleset.Value.ShortName switch
            {
                @"taiko" => (@"mode-taiko-small", @"mode-taiko"),
                @"fruits" => (@"mode-fruits-small", @"mode-fruits"),
                @"mania" => (@"mode-mania-small", @"mode-mania"),
                _ => (@"mode-osu-small", @"mode-osu"),
            };
            // mode-*-small uses internal default texture (SkinManager.LoadPermanent in osu!stable)
            modeSmallSprite.Texture = skinManager.DefaultClassicSkin.GetTexture(smallTex);
            modeWatermarkSprite.Texture = skinManager.GetClassicTexture(bigTex);
        }

        private void toggleModeMenu()
        {
            if (modeMenuOverlay.Alpha > 0f)
                modeMenuOverlay.FadeOut(150, Easing.OutQuint);
            else
                modeMenuOverlay.FadeIn(150, Easing.OutQuint);
        }

        private void selectRulesetByShortName(string shortName)
        {
            modeMenuOverlay.FadeOut(150, Easing.OutQuint);

            if (ruleset is not Bindable<RulesetInfo> mutableRuleset || mutableRuleset.Disabled)
                return;

            var target = rulesets.AvailableRulesets.FirstOrDefault(r => r.ShortName == shortName);
            if (target != null)
                mutableRuleset.Value = target;
        }

        protected override void PopIn() => this.FadeIn(200, Easing.OutQuint);
        protected override void PopOut() => this.FadeOut(200, Easing.OutQuint);

        private partial class StableSelectionSpriteButton : OsuClickableContainer
        {
            private readonly string normalTexName;
            private readonly string overTexName;
            private readonly string fallbackLabel;
            private readonly Action? rightClickAction;

            private Container normalContainer = null!;
            private Container hoverContainer = null!;
            private ISample? hoverSample;
            private ISample? clickSample;

            public Drawable? ExtraOverlay { get; init; }

            public StableSelectionSpriteButton(string normalTexName, string overTexName, string fallbackLabel, Action action, Action? rightClickAction = null)
            {
                this.normalTexName = normalTexName;
                this.overTexName = overTexName;
                this.fallbackLabel = fallbackLabel;
                this.rightClickAction = rightClickAction;

                Action = action;
                AutoSizeAxes = Axes.Both;
                Anchor = Anchor.BottomLeft;
                Origin = Anchor.BottomLeft;
            }

            [BackgroundDependencyLoader]
            private void load(AudioManager audio, SkinManager skinManager)
            {
                hoverSample = skinManager.GetClassicSample(@"menuclick") ?? audio.Samples.Get(@"UI/generic-hover");
                clickSample = skinManager.GetClassicSample(@"click-short-confirm", @"menuhit") ?? audio.Samples.Get(@"UI/generic-select");

                Children = new Drawable[]
                {
                    normalContainer = new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        AutoSizeAxes = Axes.Both,
                    },
                    ExtraOverlay ?? Empty(),
                    hoverContainer = new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        AutoSizeAxes = Axes.Both,
                        Alpha = 0,
                    },
                };

                populateSkinDrawables(skinManager);
            }

            public void UpdateSkin(SkinManager skinManager)
            {
                hoverSample = skinManager.GetClassicSample(@"menuclick");
                clickSample = skinManager.GetClassicSample(@"click-short-confirm", @"menuhit");
                populateSkinDrawables(skinManager);
            }

            private void populateSkinDrawables(SkinManager skinManager)
            {
                normalContainer.Clear();
                hoverContainer.Clear();

                var normalAnim = skinManager.GetClassicAnimation(normalTexName);
                if (normalAnim != null)
                {
                    normalAnim.Anchor = Anchor.BottomLeft;
                    normalAnim.Origin = Anchor.BottomLeft;
                    normalContainer.Add(normalAnim);
                }
                else
                {
                    normalContainer.Add(new Box
                    {
                        Size = new Vector2(74, 90),
                        Colour = Color4.Black.Opacity(0.8f),
                    });
                    normalContainer.Add(new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = fallbackLabel,
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                    });
                }

                var overAnim = skinManager.GetClassicAnimation(overTexName);
                if (overAnim != null)
                {
                    overAnim.Anchor = Anchor.BottomLeft;
                    overAnim.Origin = Anchor.BottomLeft;
                    hoverContainer.Add(overAnim);
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSample?.Play();
                hoverContainer.FadeIn(120, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverContainer.FadeOut(200, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                clickSample?.Play();
                return base.OnClick(e);
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                if (e.Button == MouseButton.Right && rightClickAction != null)
                {
                    clickSample?.Play();
                    rightClickAction.Invoke();
                    return true;
                }

                return base.OnMouseDown(e);
            }
        }

        private partial class OptionsSpriteButton : StableSelectionSpriteButton, IHasPopover
        {
            private readonly OverlayColourProvider colourProvider;
            private readonly ISongSelect? songSelect;
            private readonly IBindable<WorkingBeatmap> workingBeatmap;
            private readonly RealmAccess realm;

            public OptionsSpriteButton(
                string normalTexName,
                string overTexName,
                OverlayColourProvider colourProvider,
                ISongSelect? songSelect,
                IBindable<WorkingBeatmap> workingBeatmap,
                RealmAccess realm)
                : base(normalTexName, overTexName, @"Options", () => { })
            {
                this.colourProvider = colourProvider;
                this.songSelect = songSelect;
                this.workingBeatmap = workingBeatmap;
                this.realm = realm;

                Action = () =>
                {
                    if (workingBeatmap.IsDefault)
                        return;

                    if (this.FindClosestParent<PopoverContainer>()?.CurrentTarget == this)
                        this.HidePopover();
                    else
                        this.ShowPopover();
                };
            }

            public Framework.Graphics.UserInterface.Popover GetPopover()
            {
                var liveBeatmap = realm.Run(r => r.Find<BeatmapInfo>(workingBeatmap.Value.BeatmapInfo.ID)!.ToLive(realm));
                return new FooterButtonOptions.Popover(new FooterButtonOptions(), liveBeatmap.Value.Detach())
                {
                    ColourProvider = colourProvider,
                    SongSelect = songSelect,
                };
            }
        }

        private partial class StableStartOsuCookie : OsuClickableContainer
        {
            private const float base_scale = 1f;
            private const float beat_scale = 1.125f;
            private const float ripple_scale = 1.25f;
            private const float hover_scale = 1.375f;

            private Container mainSprite = null!;
            private Container rippleSprite = null!;
            private Container hoverOverlay = null!;
            private ISample? hoverSample;
            private ISample? clickSample;

            [Resolved]
            private TextureStore textures { get; set; } = null!;

            public StableStartOsuCookie(Action onStart)
            {
                Action = onStart;
                Size = new Vector2(216f);
            }

            [BackgroundDependencyLoader]
            private void load(AudioManager audio, SkinManager skinManager)
            {
                hoverSample = skinManager.GetClassicSample(@"menuclick") ?? audio.Samples.Get(@"UI/generic-hover");
                clickSample = skinManager.GetClassicSample(@"menuhit") ?? audio.Samples.Get(@"UI/generic-select");

                Children = new Drawable[]
                {
                    new CookieBeatSyncer(this)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    mainSprite = createLayer(),
                    rippleSprite = createLayer(),
                    hoverOverlay = createLayer(),
                };

                rippleSprite.Blending = BlendingParameters.Additive;
                rippleSprite.Alpha = 0f;
                hoverOverlay.Blending = BlendingParameters.Additive;
                hoverOverlay.Alpha = 0.01f;

                setCookie(skinCookie(skinManager));
            }

            private static Container createLayer() => new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fit,
                Scale = new Vector2(base_scale),
            };

            /// <summary>
            /// The player's own skin's <c>menu-osu</c> when it has one, otherwise null (TumThai's green cookie is drawn instead;
            /// TumThai never ships osu!'s cookie itself).
            /// </summary>
            private static Texture? skinCookie(SkinManager skinManager)
            {
                var skin = skinManager.CurrentSkin.Value;

                if (skin is LegacySkin && skin != skinManager.DefaultClassicSkin)
                    return skin.GetTexture(@"menu-osu");

                return null;
            }

            private void setCookie(Texture? skinTexture)
            {
                foreach (var layer in new[] { mainSprite, rippleSprite, hoverOverlay })
                    layer.Child = skinTexture != null ? createSkinSprite(skinTexture) : createSomtumCookie();
            }

            private static Drawable createSkinSprite(Texture texture) => new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fit,
                Texture = texture,
            };

            /// <summary>
            /// The same green cookie as TumThai's main menu logo (OsuLogo): a white ring, the green gradient and the somtum.fun logo.
            /// </summary>
            private Drawable createSomtumCookie() => new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                    new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(0.9f),
                        Colour = ColourInfo.GradientVertical(Color4Extensions.FromHex(@"86b06c"), Color4Extensions.FromHex(@"5f824b")),
                    },
                    new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        // the wordmark fills about 72% of logo.png's width: this makes it about 85% of the green circle
                        Size = new Vector2(1.06f),
                        FillMode = FillMode.Fit,
                        Texture = textures.Get(@"Menu/logo"),
                    },
                },
            };

            public void UpdateSkin(SkinManager skinManager)
            {
                hoverSample = skinManager.GetClassicSample(@"menuclick");
                clickSample = skinManager.GetClassicSample(@"menuhit");

                if (IsLoaded)
                    setCookie(skinCookie(skinManager));
            }

            private void pulseOnBeat(TimingControlPoint timingPoint)
            {
                double beatLen = Math.Clamp(timingPoint.BeatLength, 150.0, 2000.0);

                // SongSelection.cs lines 3525-3527:
                // s_Osu.ScaleTo(0.9f -> 0.45f at @2x, 0.8f -> 0.40f at @2x, beatLength, OutQuad)
                // s_Osu1.ScaleTo(0.9f -> 0.45f, 0.98f -> 0.49f, beatLength, OutQuad) + FadeOutFromOne
                mainSprite.ClearTransforms();
                mainSprite.ScaleTo(beat_scale).Then().ScaleTo(base_scale, beatLen, Easing.OutQuad);

                rippleSprite.ClearTransforms();
                rippleSprite.ScaleTo(beat_scale).FadeTo(0.65f)
                            .Then()
                            .ScaleTo(ripple_scale, beatLen, Easing.OutQuad)
                            .FadeOut(beatLen, Easing.OutQuad);
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSample?.Play();
                hoverOverlay.FadeTo(0.4f, 350, Easing.OutQuint);
                hoverOverlay.ScaleTo(hover_scale, 350, Easing.OutElastic);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverOverlay.FadeTo(0.01f, 350, Easing.OutQuint);
                hoverOverlay.ScaleTo(base_scale, 350, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                clickSample?.Play();
                return base.OnClick(e);
            }

            private partial class CookieBeatSyncer : BeatSyncedContainer
            {
                private readonly StableStartOsuCookie owner;

                public CookieBeatSyncer(StableStartOsuCookie owner)
                {
                    this.owner = owner;
                }

                protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
                {
                    base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);
                    owner.pulseOnBeat(timingPoint);
                }
            }
        }

        private partial class ModeMenuOptionRow : OsuClickableContainer
        {
            private readonly int index;
            private readonly string label;
            private readonly Texture? iconTex;

            private Box bg = null!;
            private Sprite? iconSprite;
            private ISample? hoverSample;
            private ISample? clickSample;

            public ModeMenuOptionRow(int index, string label, Texture? iconTex, Action action)
            {
                this.index = index;
                this.label = label;
                this.iconTex = iconTex;
                Action = action;
                Size = new Vector2(360f, 92f);
            }

            [BackgroundDependencyLoader]
            private void load(AudioManager audio, SkinManager skinManager)
            {
                var skin = skinManager.DefaultClassicSkin;
                hoverSample = skin.GetSample(new SampleInfo(@"menuclick")) ?? audio.Samples.Get(@"UI/generic-hover");
                clickSample = skin.GetSample(new SampleInfo(@"click-short-confirm", @"menuhit")) ?? audio.Samples.Get(@"UI/generic-select");

                Masking = true;
                CornerRadius = 6f;
                BorderThickness = 2f;
                BorderColour = Color4.White.Opacity(0.5f);

                Children = new Drawable[]
                {
                    bg = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.8f),
                    },
                    iconTex != null
                        ? (iconSprite = new Sprite
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.Centre,
                            X = 56f,
                            Texture = iconTex,
                            Scale = new Vector2(0.75f),
                            Blending = BlendingParameters.Additive,
                        })
                        : Empty(),
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 112f,
                        Text = $"{index}. {label}",
                        Font = OsuFont.GetFont(size: 28, weight: FontWeight.Bold),
                        Colour = Color4.White,
                        Shadow = true,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSample?.Play();
                bg.FadeColour(Color4Extensions.FromHex(@"0096EC").Opacity(0.75f), 150, Easing.OutQuint);
                iconSprite?.ScaleTo(0.95f, 350, Easing.OutElastic);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                bg.FadeColour(Color4.Black.Opacity(0.8f), 150, Easing.OutQuint);
                iconSprite?.ScaleTo(0.75f, 200, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                clickSample?.Play();
                return base.OnClick(e);
            }
        }
    }
}
