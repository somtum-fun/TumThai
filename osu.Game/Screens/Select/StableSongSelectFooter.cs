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
using osu.Game.Extensions;
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
                        // osu!stable has no mode watermark behind the player panel
                        Alpha = 0f,
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
                    // osu!stable: the skin's mode-*-small at its own size, centred 28.8 right of the button and 35 up from the
                    // bottom (640x480 units, x1.6 here); a skin can make it a large decoration on purpose.
                    ExtraOverlay = modeSmallSprite = new Sprite
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.Centre,
                        Position = new Vector2(28.8f * 1.6f, -35f * 1.6f),
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

        private Container customRulesetList = null!;

        private Container createModeMenuOverlay()
        {
            // osu!stable (SongSelection): the mode menu rises from the Mode button, one 80x80 row (x1.6 here) per mode stacked
            // upward from just above the bottom bar: osu!, taiko, catch, mania. TumThai!Next adds one more row above them
            // when custom rulesets are installed, which opens them in a lazer-style list (so the stable menu keeps its size).
            var modeRows = new Container
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.Both,
            };

            const float row_size = 80f * 1.6f;
            const float first_row_bottom = (480f - 424f) * 1.6f;

            var legacy = rulesets.AvailableRulesets.Where(r => r.IsLegacyRuleset()).OrderBy(r => r.OnlineID).ToList();
            var custom = rulesets.AvailableRulesets.Where(r => !r.IsLegacyRuleset()).OrderBy(r => r.Name).ToList();

            // A skin's mode-*-med is drawn at its own size, like stable. Some skins put a whole-screen artwork there (one
            // ships a 2323x2000 picture of its own menu), which buries the menu: then all four use the default icons.
            bool skinMedsFit = new[] { @"mode-osu-med", @"mode-taiko-med", @"mode-fruits-med", @"mode-mania-med" }.All(n =>
            {
                var t = skinManager.CurrentSkin.Value.GetTexture(n);
                return t == null || (t.DisplayWidth <= row_size * 2 && t.DisplayHeight <= row_size * 2);
            });

            for (int i = 0; i < legacy.Count; i++)
            {
                var r = legacy[i];
                string medTexture = r.ShortName switch
                {
                    @"taiko" => @"mode-taiko-med",
                    @"fruits" => @"mode-fruits-med",
                    @"mania" => @"mode-mania-med",
                    _ => @"mode-osu-med",
                };

                modeRows.Add(new ModeMenuOptionRow(r.Name, medTexture, skinMedsFit, null, () => selectRuleset(r))
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Position = new Vector2(224f, -(first_row_bottom + i * row_size)),
                });
            }

            var customFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding(8),
                Spacing = new Vector2(0, 4),
            };

            foreach (var r in custom)
                customFlow.Add(new CustomRulesetItem(r, ruleset, () => selectRuleset(r)));

            float moreBottom = first_row_bottom + legacy.Count * row_size;

            customRulesetList = new Container
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                // beside the "More modes" row, growing upward; capped to the space above the bottom bar
                Position = new Vector2(224f + row_size * 3.5f + 16f, -moreBottom),
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 10,
                Alpha = 0f,
                EdgeEffect = new osu.Framework.Graphics.Effects.EdgeEffectParameters
                {
                    Type = osu.Framework.Graphics.Effects.EdgeEffectType.Shadow,
                    Colour = Color4.Black.Opacity(0.4f),
                    Radius = 10,
                },
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4Extensions.FromHex(@"222a30").Opacity(0.95f),
                    },
                    new OsuScrollContainer
                    {
                        Width = 320,
                        Height = Math.Min(custom.Count * 52f + 16f, 600f),
                        Child = customFlow,
                    },
                },
            };

            if (custom.Count > 0)
            {
                modeRows.Add(new ModeMenuOptionRow(@"More modes", null, false, FontAwesome.Solid.EllipsisH, toggleCustomRulesetList)
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Position = new Vector2(224f, -moreBottom),
                });
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
                            Colour = Color4.Black.Opacity(0.5f),
                        },
                    },
                    modeRows,
                    customRulesetList,
                },
            };
        }

        private void toggleCustomRulesetList()
        {
            if (customRulesetList.Alpha > 0f)
                customRulesetList.FadeOut(150, Easing.OutQuint);
            else
                customRulesetList.FadeIn(150, Easing.OutQuint);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentSkin = skinManager.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ => updateSkin());


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
            // osu!stable's song select loads mode-*-small from the skin (only the player panel's icon is always the default one)
            modeSmallSprite.Texture = skinManager.GetClassicTexture(smallTex);
            modeSmallSprite.ScaleTo(0f).ScaleTo(1f, 1000, Easing.OutElastic);
            modeWatermarkSprite.Texture = skinManager.GetClassicTexture(bigTex);
        }

        private void toggleModeMenu()
        {
            customRulesetList.FadeOut();

            if (modeMenuOverlay.Alpha > 0f)
                modeMenuOverlay.FadeOut(150, Easing.OutQuint);
            else
                modeMenuOverlay.FadeIn(150, Easing.OutQuint);
        }

        private void selectRuleset(RulesetInfo target)
        {
            modeMenuOverlay.FadeOut(150, Easing.OutQuint);

            if (ruleset is not Bindable<RulesetInfo> mutableRuleset || mutableRuleset.Disabled)
                return;

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

            // osu!stable's default button size (77x90 at 1x): clicks and hover only count there, so a skin's oversized
            // selection-* image (drawn at its own size, as a decoration) can't cover the other buttons or the song list.
            private static readonly Vector2 hit_size = new Vector2(77f, 90f);

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            {
                Vector2 local = ToLocalSpace(screenSpacePos);
                return local.X >= 0 && local.X <= hit_size.X && local.Y <= DrawHeight && local.Y >= DrawHeight - hit_size.Y;
            }

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

            // osu!stable's ripple (0.65) and hover (0.4) are additive copies of its pink cookie; on the white-and-green
            // somtum cookie that washes it out to white, so it gets a plain faint ripple and a slight hover lift instead.
            private bool somtumCookie;

            private float ripplePeak => somtumCookie ? 0.3f : 0.65f;
            private float hoverPeak => somtumCookie ? 0.1f : 0.4f;

            private void setCookie(Texture? skinTexture)
            {
                somtumCookie = skinTexture == null;

                foreach (var layer in new[] { mainSprite, rippleSprite, hoverOverlay })
                    layer.Child = skinTexture != null ? createSkinSprite(skinTexture) : createSomtumCookie();

                rippleSprite.Blending = somtumCookie ? BlendingParameters.Inherit : BlendingParameters.Additive;
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
                rippleSprite.ScaleTo(beat_scale).FadeTo(ripplePeak)
                            .Then()
                            .ScaleTo(ripple_scale, beatLen, Easing.OutQuad)
                            .FadeOut(beatLen, Easing.OutQuad);
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSample?.Play();
                hoverOverlay.FadeTo(hoverPeak, 350, Easing.OutQuint);
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
            private const float row_size = 80f * 1.6f;

            private readonly string text;
            private readonly string? medTexture;
            private readonly bool allowSkinTexture;
            private readonly IconUsage? fontIcon;

            private Drawable icon = null!;
            private OsuSpriteText label = null!;
            private bool iconFromDefaultSkin;
            private ISample? hoverSample;
            private ISample? clickSample;

            public ModeMenuOptionRow(string text, string? medTexture, bool allowSkinTexture, IconUsage? fontIcon, Action action)
            {
                this.text = text;
                this.medTexture = medTexture;
                this.allowSkinTexture = allowSkinTexture;
                this.fontIcon = fontIcon;
                Action = action;
                // osu!stable's row: 80 tall, 3.5x as wide (in 640x480 units)
                Size = new Vector2(row_size * 3.5f, row_size);
            }

            [BackgroundDependencyLoader]
            private void load(AudioManager audio, SkinManager skinManager)
            {
                hoverSample = skinManager.GetClassicSample(@"click-short") ?? audio.Samples.Get(@"UI/generic-hover");
                clickSample = skinManager.GetClassicSample(@"click-short-confirm", @"menuhit") ?? audio.Samples.Get(@"UI/generic-select");

                if (medTexture != null)
                {
                    // the skin's mode-*-med at its own size (a skin may make it tiny or a big decoration on purpose)
                    var userSkin = skinManager.CurrentSkin.Value;
                    var own = allowSkinTexture && userSkin is LegacySkin && userSkin != skinManager.DefaultClassicSkin ? userSkin.GetTexture(medTexture) : null;
                    iconFromDefaultSkin = own == null;

                    icon = new Sprite
                    {
                        Texture = own ?? skinManager.DefaultClassicSkin.GetTexture(medTexture),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        X = row_size / 2,
                    };
                }
                else
                {
                    icon = new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        X = row_size / 2,
                        Size = new Vector2(row_size * 0.45f),
                        Icon = fontIcon ?? FontAwesome.Solid.EllipsisH,
                        Shadow = true,
                    };
                    iconFromDefaultSkin = true;
                }

                Children = new Drawable[]
                {
                    icon,
                    label = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = row_size + 16f,
                        Text = text,
                        Font = OsuFont.GetFont(size: 24f * 1.6f),
                        Colour = Color4.White,
                        Shadow = true,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSample?.Play();
                if (iconFromDefaultSkin)
                    icon.ScaleTo(1.1f, 1000, Easing.OutElastic);
                label.MoveToX(row_size + 32f, 300, Easing.Out);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (iconFromDefaultSkin)
                    icon.ScaleTo(1f, 1000, Easing.OutElastic);
                label.MoveToX(row_size + 16f, 300, Easing.Out);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                clickSample?.Play();
                return base.OnClick(e);
            }
        }

        /// <summary>
        /// A custom ruleset in the lazer-style list the mode menu's "More modes" row opens.
        /// </summary>
        private partial class CustomRulesetItem : OsuClickableContainer
        {
            private readonly RulesetInfo rulesetInfo;
            private readonly IBindable<RulesetInfo> current;
            private Box hover = null!;
            private Box selected = null!;

            public CustomRulesetItem(RulesetInfo rulesetInfo, IBindable<RulesetInfo> current, Action action)
            {
                this.rulesetInfo = rulesetInfo;
                this.current = current.GetBoundCopy();
                Action = action;
                Size = new Vector2(304, 48);
                Masking = true;
                CornerRadius = 6;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                Children = new Drawable[]
                {
                    selected = new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Blue3.Opacity(0.5f), Alpha = 0 },
                    hover = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.1f), Alpha = 0 },
                    new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 12,
                        Size = new Vector2(28),
                        Child = rulesetInfo.CreateInstance().CreateIcon().With(d =>
                        {
                            d.RelativeSizeAxes = Axes.Both;
                            d.Anchor = Anchor.Centre;
                            d.Origin = Anchor.Centre;
                        }),
                    },
                    new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 52,
                        Text = rulesetInfo.Name,
                        Font = OsuFont.GetFont(size: 20, weight: FontWeight.SemiBold),
                        Width = 240,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                current.BindValueChanged(c => selected.FadeTo(c.NewValue.Equals(rulesetInfo) ? 1 : 0, 150), true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                hover.FadeIn(100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hover.FadeOut(300);
                base.OnHoverLost(e);
            }
        }
    }
}
