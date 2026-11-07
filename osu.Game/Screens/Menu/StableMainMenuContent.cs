// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Skinning;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// osu!stable Main Menu chrome and vertical right-hand menu buttons (matching Menu.cs + MusicControl.cs):
    /// - Top &amp; bottom translucent black bars (height 68 top / 54 bottom, 45% black)
    /// - Top-left <see cref="StableUserPanel"/> + beatmap count / uptime / clock text (visible when menu is expanded)
    /// - Top-right Now Playing banner (<c>menu-np</c> positioned with "Now Playing" graphic immediately left of track text),
    ///   7 plain white FontAwesome transport icons, and clickable track seekbar below them
    /// - Bottom-left <c>menu-copyright</c> sprite and bottom-right <c>menu-subscriber</c> heart + Online Users / Show Chat tabs
    /// - Right-edge osu!direct tab (<c>menu-osudirect</c> / <c>menu-osudirect-over</c>)
    /// - Vertical stack of osu!stable pill buttons (Play, Edit, Options, Exit / Solo, Multi, Back)
    ///   proxied behind the osu! cookie.
    /// </summary>
    public partial class StableMainMenuContent : CompositeDrawable
    {
        private readonly ButtonSystem buttonSystem;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private BeatmapListingOverlay? beatmapListing { get; set; }

        [Resolved]
        private SettingsOverlay? settingsOverlay { get; set; }

        [Resolved]
        private ChatOverlay? chatOverlay { get; set; }

        [Resolved]
        private DashboardOverlay? dashboardOverlay { get; set; }

        [Resolved]
        private NowPlayingOverlay? nowPlayingOverlay { get; set; }

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private IBindable<Skin> currentSkin = null!;

        private OsuSpriteText beatmapCountText = null!;
        private OsuSpriteText uptimeText = null!;
        private OsuSpriteText clockText = null!;
        private OsuSpriteText nowPlayingText = null!;
        private Box musicProgressFill = null!;
        private Sprite? npSprite;
        private FillFlowContainer statsTextFlow = null!;
        private OsuDirectTab osuDirectTab = null!;

        private readonly Container buttonsRootContainer;
        private FillFlowContainer topLevelStack = null!;
        private FillFlowContainer playLevelStack = null!;
        private FillFlowContainer editLevelStack = null!;

        private readonly Stopwatch sessionStopwatch = Stopwatch.StartNew();

        public StableMainMenuContent(ButtonSystem buttonSystem)
        {
            this.buttonSystem = buttonSystem;
            RelativeSizeAxes = Axes.Both;

            buttonsRootContainer = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.CentreLeft,
                X = -160,
                AutoSizeAxes = Axes.Both,
            };
        }

        /// <summary>
        /// Creates a drawable proxy for the menu buttons so they render behind the main <see cref="OsuLogo"/>.
        /// </summary>
        public Drawable CreateButtonsProxy() => buttonsRootContainer.CreateProxy();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            var npTex = skinManager.GetClassicTexture(@"menu-np");
            var osuDirectTex = skinManager.GetClassicTexture(@"menu-osudirect");
            var osuDirectOverTex = skinManager.GetClassicTexture(@"menu-osudirect-over");

            buttonsRootContainer.Children = new Drawable[]
            {
                topLevelStack = new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new StableMenuPillButton(@"menu-button-play", @"menu-play-click", "Play", Color4Extensions.FromHex(@"6844CC"), () => buttonSystem.State = ButtonSystemState.Play),
                        new StableMenuPillButton(@"menu-button-edit", @"menu-edit-click", "Edit", Color4Extensions.FromHex(@"E96800"), () => buttonSystem.OnEditBeatmap?.Invoke()),
                        new StableMenuPillButton(@"menu-button-options", @"menuhit", "Options", Color4Extensions.FromHex(@"0096EC"), () => settingsOverlay?.ToggleVisibility()),
                        new StableMenuPillButton(@"menu-button-exit", @"seeya", "Exit", Color4Extensions.FromHex(@"EE3399"), () => buttonSystem.OnExit?.Invoke(null!)),
                    },
                },
                playLevelStack = new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new StableMenuPillButton(@"menu-button-freeplay", @"menu-freeplay-click", "Solo", Color4Extensions.FromHex(@"6844CC"), () => buttonSystem.OnSolo?.Invoke()),
                        new StableMenuPillButton(@"menu-button-multiplayer", @"menu-multiplayer-click", "Multi", Color4Extensions.FromHex(@"0096EC"), () => buttonSystem.OnMultiplayer?.Invoke()),
                        new StableMenuPillButton(@"menu-button-back", @"menuback", "Back", Color4Extensions.FromHex(@"EE3399"), () => buttonSystem.State = ButtonSystemState.TopLevel),
                    },
                },
                editLevelStack = new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new StableMenuPillButton(@"menu-button-edit", @"menu-edit-click", "Beatmap", Color4Extensions.FromHex(@"E96800"), () => buttonSystem.OnEditBeatmap?.Invoke()),
                        new StableMenuPillButton(@"menu-button-options", @"menuhit", "Skin", Color4Extensions.FromHex(@"D48800"), () => buttonSystem.OnEditSkin?.Invoke()),
                        new StableMenuPillButton(@"menu-button-back", @"menuback", "Back", Color4Extensions.FromHex(@"EE3399"), () => buttonSystem.State = ButtonSystemState.TopLevel),
                    },
                },
            };

            InternalChildren = new Drawable[]
            {
                // Top letterbox bar (Menu.cs topBehind: 45% black, no border)
                new Container
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 68,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.45f),
                        },
                        new Container
                        {
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.TopLeft,
                            AutoSizeAxes = Axes.Both,
                            Child = new StableUserPanel(),
                        },
                        statsTextFlow = new FillFlowContainer
                        {
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.TopLeft,
                            Position = new Vector2(335, 6),
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 2),
                            Alpha = 0,
                            Children = new Drawable[]
                            {
                                beatmapCountText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.Regular),
                                    Colour = Color4.White,
                                    Shadow = true,
                                },
                                uptimeText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.Regular),
                                    Colour = Color4.White,
                                    Shadow = true,
                                },
                                clockText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.Regular),
                                    Colour = Color4.White,
                                    Shadow = true,
                                },
                            },
                        },
                        // Top-right MusicControl (MusicControl.cs lines 87-192, 524-566)
                        new Container
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Size = new Vector2(500, 68),
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    RelativeSizeAxes = Axes.X,
                                    Height = 28,
                                    Children = new Drawable[]
                                    {
                                        npTex != null
                                            ? (npSprite = new Sprite
                                            {
                                                Anchor = Anchor.TopRight,
                                                Origin = Anchor.TopLeft,
                                                Texture = npTex,
                                            })
                                            : Empty(),
                                        nowPlayingText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreRight,
                                            Origin = Anchor.CentreRight,
                                            Margin = new MarginPadding { Right = 10 },
                                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.Regular),
                                            Colour = Color4.White,
                                            Shadow = true,
                                        },
                                    },
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    Position = new Vector2(-8, 30),
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(12, 0),
                                    Children = new Drawable[]
                                    {
                                        new TransportIconButton(FontAwesome.Solid.StepBackward, () => musicController.PreviousTrack()),
                                        new TransportIconButton(FontAwesome.Solid.Play, () => musicController.Play(requestedByUser: true)),
                                        new TransportIconButton(FontAwesome.Solid.Pause, () => musicController.Stop(requestedByUser: true)),
                                        new TransportIconButton(FontAwesome.Solid.Stop, () => musicController.Stop(requestedByUser: true)),
                                        new TransportIconButton(FontAwesome.Solid.StepForward, () => musicController.NextTrack()),
                                        new TransportIconButton(FontAwesome.Solid.InfoCircle, () => beatmapListing?.ToggleVisibility()),
                                        new TransportIconButton(FontAwesome.Solid.Bars, () => nowPlayingOverlay?.ToggleVisibility()),
                                    },
                                },
                                new MusicSeekBar(musicController, out musicProgressFill)
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    Position = new Vector2(-8, 54),
                                    Size = new Vector2(204, 3),
                                },
                            },
                        },
                    },
                },
                // Bottom letterbox bar with bottom-right tabs (menu-copyright and menu-subscriber removed per TumThai!Next design)
                new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 54,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.45f),
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight,
                            Position = new Vector2(-8, 0),
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(6, 0),
                            Children = new Drawable[]
                            {
                                new StableBottomTabButton("Online Users", () => dashboardOverlay?.ToggleVisibility()),
                                new StableBottomTabButton("^ Show Chat", () => chatOverlay?.ToggleVisibility()),
                            },
                        },
                    },
                },
                // Right-edge osu!direct tab using genuine menu-osudirect & menu-osudirect-over sprites
                osuDirectTab = new OsuDirectTab(osuDirectTex, osuDirectOverTex, () => beatmapListing?.ToggleVisibility())
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                },
                buttonsRootContainer,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            int setCount = realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.DeletePending && !s.Protected));
            beatmapCountText.Text = $"You have {setCount:N0} beatmaps available!";

            beatmap.BindValueChanged(b =>
            {
                var meta = b.NewValue.Metadata;
                nowPlayingText.Text = $"{meta.Artist} - {meta.Title}";
            }, true);

            buttonSystem.StateChanged += updateMenuState;
            updateMenuState(buttonSystem.State);

            currentSkin = skinManager.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ =>
            {
                if (npSprite != null)
                    npSprite.Texture = skinManager.GetClassicTexture(@"menu-np");

                osuDirectTab.UpdateSkin(skinManager);

                foreach (var stack in new[] { topLevelStack, playLevelStack, editLevelStack })
                {
                    foreach (var btn in stack.Children.OfType<StableMenuPillButton>())
                        btn.UpdateSkin(skinManager);
                }
            });
        }

        private void updateMenuState(ButtonSystemState state)
        {
            bool isExpanded = state == ButtonSystemState.TopLevel || state == ButtonSystemState.Play || state == ButtonSystemState.Multi || state == ButtonSystemState.Edit;
            statsTextFlow.FadeTo(isExpanded ? 1f : 0f, 300, Easing.OutQuint);

            showStack(topLevelStack, state == ButtonSystemState.TopLevel);
            showStack(playLevelStack, state == ButtonSystemState.Play || state == ButtonSystemState.Multi);
            showStack(editLevelStack, state == ButtonSystemState.Edit);
        }

        private static void showStack(FillFlowContainer stack, bool visible)
        {
            if (visible)
            {
                stack.MoveToX(-40).FadeIn(220, Easing.OutQuint).MoveToX(0, 320, Easing.OutBack);
            }
            else
            {
                stack.FadeOut(160, Easing.OutQuint).MoveToX(-30, 160, Easing.OutQuint);
            }
        }

        private double lastClockUpdate;

        protected override void Update()
        {
            base.Update();

            if (Time.Current - lastClockUpdate > 500)
            {
                lastClockUpdate = Time.Current;
                var elapsed = sessionStopwatch.Elapsed;
                string uptimeStr = elapsed.TotalHours >= 1
                    ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m"
                    : $"{Math.Max(1, elapsed.Minutes)} minutes";
                uptimeText.Text = $"TumThai!Next has been running for {uptimeStr}.";
                clockText.Text = $"It is currently {DateTime.Now:h:mm tt}.";
            }

            if (npSprite != null)
            {
                // MusicControl.cs line 526: position menu-np so its "Now Playing" graphic sits immediately left of nowPlayingText
                npSprite.X = -(nowPlayingText.DrawWidth + 128);
            }

            var track = musicController.CurrentTrack;
            if (track.Length > 0)
                musicProgressFill.Width = (float)Math.Clamp(track.CurrentTime / track.Length, 0, 1);
            else
                musicProgressFill.Width = 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            buttonSystem.StateChanged -= updateMenuState;
        }

        private partial class MusicSeekBar : OsuClickableContainer
        {
            private readonly MusicController musicController;

            public MusicSeekBar(MusicController musicController, out Box progressFill)
            {
                this.musicController = musicController;
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        // a faint track, not a black block: it read as a stray bar while the song had barely started
                        Colour = Color4.White.Opacity(0.15f),
                    },
                    progressFill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Width = 0,
                        Colour = Color4.White.Opacity(0.8f),
                    },
                };
            }

            protected override bool OnClick(ClickEvent e)
            {
                var track = musicController.CurrentTrack;
                if (track.Length > 0 && DrawWidth > 0)
                {
                    double fraction = Math.Clamp(e.MousePosition.X / DrawWidth, 0, 1);
                    musicController.SeekTo(track.Length * fraction);
                }

                return true;
            }
        }

        private partial class StableBottomTabButton : OsuClickableContainer
        {
            private readonly string text;
            private Box bg = null!;

            public StableBottomTabButton(string text, Action action)
            {
                this.text = text;
                Action = action;
                AutoSizeAxes = Axes.X;
                Height = 18;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Masking = true;
                CornerRadius = 3;
                Children = new Drawable[]
                {
                    bg = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4Extensions.FromHex(@"1A2855").Opacity(0.85f),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Margin = new MarginPadding { Horizontal = 12 },
                        Text = text,
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                        Colour = Color4.White,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                bg.FadeColour(Color4Extensions.FromHex(@"2A4895"), 120, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                bg.FadeColour(Color4Extensions.FromHex(@"1A2855").Opacity(0.85f), 150, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }

        private partial class StableMenuPillButton : OsuClickableContainer
        {
            private readonly string spriteName;
            private readonly string sampleName;
            private readonly string label;
            private readonly Color4 baseColour;

            private Container body = null!;
            private Sprite? normalSprite;
            private Sprite? overSprite;
            private Box? flashBox;
            private ISample? hoverSample;
            private ISample? clickSample;

            public StableMenuPillButton(string spriteName, string sampleName, string label, Color4 baseColour, Action action)
            {
                this.spriteName = spriteName;
                this.sampleName = sampleName;
                this.label = label;
                this.baseColour = baseColour;
                Action = action;

                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(AudioManager audio, SkinManager skinManager)
            {
                hoverSample = skinManager.GetClassicSample(@"menuclick") ?? audio.Samples.Get(@"UI/generic-hover");
                clickSample = skinManager.GetClassicSample(sampleName, @"menuhit") ?? audio.Samples.Get(@"UI/generic-select");

                var normalTex = skinManager.GetClassicTexture(spriteName);
                var overTex = skinManager.GetClassicTexture($"{spriteName}-over");

                if (normalTex != null)
                {
                    Child = body = new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            normalSprite = new Sprite
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Texture = normalTex,
                            },
                            overSprite = new Sprite
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Texture = overTex ?? normalTex,
                                Alpha = 0,
                            },
                        },
                    };
                }
                else
                {
                    Child = body = new Container
                    {
                        Size = new Vector2(360, 66),
                        Masking = true,
                        CornerRadius = 10,
                        BorderThickness = 2.5f,
                        BorderColour = Color4.White.Opacity(0.9f),
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = ColourInfo.GradientVertical(baseColour.Lighten(0.2f), baseColour.Darken(0.2f)),
                            },
                            flashBox = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.White,
                                Blending = BlendingParameters.Additive,
                                Alpha = 0,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                X = 18,
                                Text = label.ToUpperInvariant(),
                                Font = OsuFont.GetFont(size: 28, weight: FontWeight.Bold),
                                Colour = Color4.White,
                                Shadow = true,
                            },
                        },
                    };
                }
            }

            public void UpdateSkin(SkinManager skinManager)
            {
                hoverSample = skinManager.GetClassicSample(@"menuclick");
                clickSample = skinManager.GetClassicSample(sampleName, @"menuhit");

                var normalTex = skinManager.GetClassicTexture(spriteName);
                var overTex = skinManager.GetClassicTexture($"{spriteName}-over");
                if (normalSprite != null && normalTex != null)
                    normalSprite.Texture = normalTex;
                if (overSprite != null && (overTex ?? normalTex) != null)
                    overSprite.Texture = overTex ?? normalTex;
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSample?.Play();
                body.MoveToX(35.2f, 350, Easing.OutElastic);
                overSprite?.FadeIn(80, Easing.OutQuint);
                flashBox?.FadeTo(0.18f, 120, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                body.MoveToX(0, 300, Easing.OutElastic);
                overSprite?.FadeOut(300, Easing.OutQuint);
                flashBox?.FadeOut(200, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                clickSample?.Play();
                return base.OnClick(e);
            }
        }

        private partial class TransportIconButton : OsuClickableContainer
        {
            private readonly IconUsage icon;
            private SpriteIcon spriteIcon = null!;
            private ISample? clickSample;

            public TransportIconButton(IconUsage icon, Action action)
            {
                this.icon = icon;
                Action = action;
                Size = new Vector2(18, 18);
            }

            [BackgroundDependencyLoader]
            private void load(AudioManager audio, SkinManager skinManager)
            {
                clickSample = skinManager.GetClassicSample(@"click-short", @"menuhit") ?? audio.Samples.Get(@"UI/generic-select");

                Child = spriteIcon = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(15),
                    Icon = icon,
                    Colour = Color4.White,
                    Shadow = true,
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                spriteIcon.ScaleTo(1.2f, 150, Easing.OutElastic);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                spriteIcon.ScaleTo(1f, 150, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                clickSample?.Play();
                return base.OnClick(e);
            }
        }

        private partial class OsuDirectTab : OsuClickableContainer
        {
            private readonly Texture? normalTex;
            private readonly Texture? overTex;
            private Container tabBody = null!;
            private Sprite? normalSprite;
            private Sprite? overSprite;
            private ISample? hoverSample;
            private ISample? clickSample;

            public OsuDirectTab(Texture? normalTex, Texture? overTex, Action action)
            {
                this.normalTex = normalTex;
                this.overTex = overTex;
                Action = action;
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(AudioManager audio, SkinManager skinManager)
            {
                hoverSample = skinManager.GetClassicSample(@"menuclick") ?? audio.Samples.Get(@"UI/generic-hover");
                clickSample = skinManager.GetClassicSample(@"click-short-confirm", @"menuhit") ?? audio.Samples.Get(@"UI/generic-select");

                if (normalTex != null)
                {
                    Child = tabBody = new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        X = 8,
                        Children = new Drawable[]
                        {
                            normalSprite = new Sprite
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Texture = normalTex,
                            },
                            overSprite = new Sprite
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Texture = overTex ?? normalTex,
                                Alpha = 0,
                            },
                        },
                    };
                }
                else
                {
                    Child = tabBody = new Container
                    {
                        Size = new Vector2(115, 96),
                        X = 12,
                        Masking = true,
                        CornerRadius = 8,
                        BorderThickness = 2,
                        BorderColour = Color4Extensions.FromHex(@"A3F02C"),
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4Extensions.FromHex(@"3E6B12").Opacity(0.92f),
                        },
                    };
                }
            }

            public void UpdateSkin(SkinManager skinManager)
            {
                hoverSample = skinManager.GetClassicSample(@"menuclick");
                clickSample = skinManager.GetClassicSample(@"click-short-confirm", @"menuhit");

                var nTex = skinManager.GetClassicTexture(@"menu-osudirect");
                var oTex = skinManager.GetClassicTexture(@"menu-osudirect-over");
                if (normalSprite != null && nTex != null)
                    normalSprite.Texture = nTex;
                if (overSprite != null && (oTex ?? nTex) != null)
                    overSprite.Texture = oTex ?? nTex;
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSample?.Play();
                tabBody.MoveToX(0, 200, Easing.OutElastic);
                overSprite?.FadeIn(120, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                tabBody.MoveToX(8, 200, Easing.OutQuint);
                overSprite?.FadeOut(150, Easing.OutQuint);
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
