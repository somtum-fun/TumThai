// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings
{
    public partial class SettingsSidebar : ExpandingContainer
    {
        public const float CONTRACTED_WIDTH = 60;
        public const int EXPANDED_WIDTH = 170;

        public Action? BackButtonAction;

        protected override bool ExpandOnHover => false;

        private readonly bool showBackButton;
        private Box background = null!;
        private BackButton? backButton;
        private IBindable<InterfaceTheme> interfaceTheme = null!;

        public SettingsSidebar(bool showBackButton)
            : base(CONTRACTED_WIDTH, EXPANDED_WIDTH)
        {
            this.showBackButton = showBackButton;
            Expanded.Value = true;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuConfigManager config)
        {
            interfaceTheme = config.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);

            AddInternal(background = new Box
            {
                Colour = colourProvider.Background5,
                RelativeSizeAxes = Axes.Both,
                Depth = float.MaxValue
            });

            if (showBackButton)
            {
                AddInternal(backButton = new BackButton
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Action = () => BackButtonAction?.Invoke(),
                });
            }

            interfaceTheme.BindValueChanged(t =>
            {
                bool isThemed = t.NewValue.IsThemed();
                Expanded.Value = !isThemed;
                background.Colour = isThemed ? Color4.Black : colourProvider.Background5;
                if (backButton != null)
                    backButton.Alpha = isThemed ? 0 : 1;
            }, true);
        }

        public partial class BackButton : SidebarButton
        {
            private Drawable content = null!;

            public BackButton()
                : base(HoverSampleSet.Default)
            {
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Size = new Vector2(EXPANDED_WIDTH);

                Padding = new MarginPadding(40);

                AddRange(new[]
                {
                    content = new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Direction = FillDirection.Vertical,
                        AutoSizeAxes = Axes.Both,
                        Spacing = new Vector2(5),
                        Children = new Drawable[]
                        {
                            new SpriteIcon
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Size = new Vector2(30),
                                Shadow = true,
                                Icon = FontAwesome.Solid.ChevronLeft
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.Regular),
                                Text = CommonStrings.Back.ToLower(),
                            },
                        }
                    }
                });
            }

            protected override void UpdateState()
            {
                base.UpdateState();

                content.FadeColour(IsHovered ? ColourProvider.Light1 : ColourProvider.Light3, FADE_DURATION, Easing.OutQuint);
            }
        }
    }
}
