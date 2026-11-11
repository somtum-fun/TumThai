// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
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
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Performance;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Users
{
    /// <summary>
    /// TumThai!Next: osu!stable user profile box (<c>User.cs</c> top-left / song select footer panel).
    /// </summary>
    public partial class StableUserPanel : OsuClickableContainer
    {
        public static readonly Vector2 DEFAULT_SIZE = new Vector2(330f, 86f);

        private readonly bool opaqueBackground;

        private Box hoverLayer = null!;
        private Drawable? borderSprite;
        private UpdateableAvatar avatar = null!;
        private OsuSpriteText usernameText = null!;
        private OsuSpriteText line1Text = null!;
        private OsuSpriteText line2Text = null!;
        private OsuSpriteText levelText = null!;
        private OsuSpriteText rankText = null!;
        private Sprite modeSprite = null!;
        private Drawable levelBarFill = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private LocalUserStatisticsProvider? statisticsProvider { get; set; }

        [Resolved(canBeNull: true)]
        private UserProfileOverlay? userProfile { get; set; }

        [Resolved(canBeNull: true)]
        private LoginOverlay? loginOverlay { get; set; }

        private IBindable<APIUser> localUser = null!;
        private IBindable<APIState> apiState = null!;
        private IBindable<Skin> currentSkin = null!;

        private const float level_bar_max_width = 180f;

        public StableUserPanel(bool opaqueBackground = false)
        {
            this.opaqueBackground = opaqueBackground;
            Size = DEFAULT_SIZE;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var userBgTex = skinManager.GetClassicTexture(@"user-bg");
            var userBorderTex = skinManager.GetClassicTexture(@"user-border");
            var levelBarBgTex = skinManager.GetClassicTexture(@"levelbar-bg");
            var levelBarTex = skinManager.GetClassicTexture(@"levelbar");

            Masking = true;

            // osu!stable's own player panel (User.cs): user-bg in (1, 1, 1) at 200/255 and user-border in black.
            Color4 stableBg = new Color4(1, 1, 1, (byte)(opaqueBackground ? 255 : 200));

            Children = new Drawable[]
            {
                userBgTex != null
                    ? new Sprite
                    {
                        Texture = userBgTex,
                        RelativeSizeAxes = Axes.Both,
                        Size = Vector2.One,
                        Colour = stableBg,
                    }
                    : new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = stableBg,
                    },
                hoverLayer = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4Extensions.FromHex("292929"),
                    Blending = BlendingParameters.Additive,
                    Alpha = 0f,
                },
                userBorderTex != null
                    ? (borderSprite = new Sprite
                    {
                        Texture = userBorderTex,
                        RelativeSizeAxes = Axes.Both,
                        Size = Vector2.One,
                        Colour = Color4.Black,
                    })
                    : Empty(),
                new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(68f),
                    Margin = new MarginPadding { Left = 4f },
                    Masking = true,
                    CornerRadius = 2f,
                    Child = avatar = new UpdateableAvatar(isInteractive: false)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                },
                modeSprite = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-12f, 4f),
                    Scale = new Vector2(0.85f),
                    Colour = Color4.White.Opacity(0.28f),
                },
                rankText = new OsuSpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Position = new Vector2(-8f, 0f),
                    Font = OsuFont.GetFont(size: 40, weight: FontWeight.Light),
                    Colour = Color4.White.Opacity(0.39f),
                },
                usernameText = new OsuSpriteText
                {
                    Position = new Vector2(78f, 3f),
                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.SemiBold),
                    Colour = Color4.White,
                    Shadow = true,
                },
                line1Text = new OsuSpriteText
                {
                    Position = new Vector2(78f, 24f),
                    Font = OsuFont.GetFont(size: 14),
                    Colour = Color4.White,
                    Shadow = true,
                },
                line2Text = new OsuSpriteText
                {
                    Position = new Vector2(78f, 39f),
                    Font = OsuFont.GetFont(size: 14),
                    Colour = Color4.White,
                    Shadow = true,
                },
                levelText = new OsuSpriteText
                {
                    Position = new Vector2(78f, 54f),
                    Font = OsuFont.GetFont(size: 14),
                    Colour = Color4.White,
                    Shadow = true,
                },
                new Container
                {
                    Position = new Vector2(124f, 60f),
                    Size = new Vector2(level_bar_max_width, 10f),
                    Children = new Drawable[]
                    {
                        levelBarBgTex != null
                            ? new Sprite
                            {
                                Texture = levelBarBgTex,
                                Size = new Vector2(level_bar_max_width, 10f),
                            }
                            : new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.White.Opacity(0.25f),
                            },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 0f,
                            Masking = true,
                            Child = levelBarFill = levelBarTex != null
                                ? new Sprite
                                {
                                    Texture = levelBarTex,
                                    Size = new Vector2(level_bar_max_width, 10f),
                                    Colour = new Color4(252, 184, 6, 255),
                                }
                                : new Box
                                {
                                    Size = new Vector2(level_bar_max_width, 10f),
                                    Colour = Color4Extensions.FromHex("FCB806"),
                                },
                        }.With(c => levelBarFill = c),
                    }
                }
            };

            Action = () =>
            {
                if (api.IsLoggedIn)
                    userProfile?.ShowUser(api.LocalUser.Value);
                else
                    loginOverlay?.Show();
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            localUser = api.LocalUser.GetBoundCopy();
            localUser.BindValueChanged(_ => Schedule(updateDisplay));

            apiState = api.State.GetBoundCopy();
            apiState.BindValueChanged(_ => Schedule(updateDisplay));

            currentSkin = skinManager.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ => Schedule(updateDisplay));

            ruleset.BindValueChanged(_ => Schedule(updateDisplay), true);

            if (statisticsProvider != null)
                statisticsProvider.StatisticsUpdated += onStatisticsUpdated;
        }

        private void onStatisticsUpdated(UserStatisticsUpdate update) => Schedule(updateDisplay);

        private void updateDisplay()
        {
            var user = localUser.Value;
            avatar.User = user;

            string modeSpriteName = ruleset.Value.OnlineID switch
            {
                0 => @"mode-osu-small",
                1 => @"mode-taiko-small",
                2 => @"mode-fruits-small",
                3 => @"mode-mania-small",
                _ => @"mode-osu-small",
            };
            // osu!stable User.cs lines 693-700 uses SkinManager.LoadPermanent (internal default texture only, never user skin)
            modeSprite.Texture = skinManager.DefaultClassicSkin.GetTexture(modeSpriteName);

            if (!api.IsLoggedIn || user == null || user.Id <= 1)
            {
                usernameText.Text = user?.Username ?? "Guest";
                line1Text.Text = "Click to sign in";
                line2Text.Text = string.Empty;
                levelText.Text = string.Empty;
                rankText.Text = string.Empty;
                levelBarFill.Width = 0f;
                return;
            }

            usernameText.Text = user.Username;

            var stats = statisticsProvider?.GetStatisticsFor(ruleset.Value) ?? user.Statistics;
            if (stats != null)
            {
                decimal pp = stats.PP ?? 0;
                line1Text.Text = $"Performance:{pp:#,0}pp";
                line2Text.Text = $"Accuracy:{stats.Accuracy:0.00}%";
                levelText.Text = $"Lv{stats.Level.Current}";

                float progress = Math.Clamp(stats.Level.Progress / 100f, 0f, 1f);
                levelBarFill.ResizeWidthTo(level_bar_max_width * progress, PotatoMode.UltraActive ? 0 : 300, Easing.OutQuint);

                if (stats.GlobalRank is int rank && rank > 0)
                {
                    rankText.Text = $"#{rank}";
                    // User.cs: the rank's colour by how high it is
                    rankText.Colour = rank switch
                    {
                        1 => new Color4(88, 171, 248, 120),
                        <= 10 => new Color4(244, 218, 73, 120),
                        <= 1000 => new Color4(255, 255, 255, 100),
                        <= 50000 => new Color4(255, 255, 255, 80),
                        <= 100000 => new Color4(255, 255, 255, 60),
                        <= 200000 => new Color4(255, 255, 255, 40),
                        _ => new Color4(255, 255, 255, 20),
                    };
                }
                else
                {
                    rankText.Text = string.Empty;
                }
            }
            else
            {
                line1Text.Text = "Performance:0pp";
                line2Text.Text = "Accuracy:0.00%";
                levelText.Text = "Lv1";
                rankText.Text = string.Empty;
                levelBarFill.Width = 0f;
            }
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverLayer.FadeTo(0.8f, PotatoMode.UltraActive ? 0 : 150, Easing.OutQuad);
            borderSprite?.FadeColour(Color4.White, PotatoMode.UltraActive ? 0 : 300, Easing.OutQuad);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverLayer.FadeOut(PotatoMode.UltraActive ? 0 : 200, Easing.OutQuad);
            borderSprite?.FadeColour(Color4.Black, PotatoMode.UltraActive ? 0 : 300, Easing.OutQuad);
            base.OnHoverLost(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (statisticsProvider != null)
                statisticsProvider.StatisticsUpdated -= onStatisticsUpdated;
        }
    }
}
