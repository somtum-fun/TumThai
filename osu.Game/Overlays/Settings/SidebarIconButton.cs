// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osuTK;
using osuTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.Containers;

namespace osu.Game.Overlays.Settings
{
    public partial class SidebarIconButton : SidebarButton
    {
        private const float selection_indicator_height_active = 18;
        private const float selection_indicator_height_inactive = 4;

        private CircularContainer selectionIndicator;
        private Box stableSelectionBar;
        private Container textIconContent;
        private ConstrainedIconContainer iconContainer;
        private OsuSpriteText headerText;
        private IBindable<InterfaceTheme> interfaceTheme;

        // always consider as part of flow, even when not visible (for the sake of the initial animation).
        public override bool IsPresent => !(interfaceTheme?.Value.IsThemed() == true && Section.UseSmallerSidebarButton);

        public required SettingsSection Section { get; init; }

        private bool selected;

        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;

                if (IsLoaded)
                    UpdateState();
            }
        }

        public SidebarIconButton()
        {
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            interfaceTheme = config.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);

            const float icon_size = 20;

            float scale = Section.UseSmallerSidebarButton ? 0.8f : 1;

            Height = 46 * scale;
            Padding = new MarginPadding { Horizontal = 5 + (Section.UseSmallerSidebarButton ? icon_size : 0), Vertical = 2.5f * scale };

            AddRange(new Drawable[]
            {
                textIconContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = OsuColour.Gray(0.6f),
                    Children = new Drawable[]
                    {
                        iconContainer = new ConstrainedIconContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.Centre,
                            X = icon_size,
                            Size = new Vector2(icon_size * scale),
                            Icon = Section.CreateIcon(),
                            Margin = new MarginPadding { Left = 25 }
                        },
                        headerText = new OsuSpriteText
                        {
                            Text = Section.Header,
                            Font = OsuFont.Default.With(size: OsuFont.DEFAULT_FONT_SIZE * scale),
                            Position = new Vector2(50, 0),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                    }
                },
                selectionIndicator = new CircularContainer
                {
                    Colour = ColourProvider.Highlight1,

                    Alpha = 0,
                    Width = 4,
                    Height = selection_indicator_height_inactive,
                    Masking = true,
                    CornerRadius = 1.5f,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Margin = new MarginPadding
                    {
                        Left = 9,
                    },
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White
                    }
                },
                stableSelectionBar = new Box
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    RelativeSizeAxes = Axes.Y,
                    Width = 5,
                    Colour = Color4Extensions.FromHex(@"EB459E"),
                    Alpha = 0,
                },
            });

            interfaceTheme.BindValueChanged(_ => UpdateState(), true);
        }

        protected override void UpdateState()
        {
            base.UpdateState();

            const float icon_size = 20;
            float scale = Section.UseSmallerSidebarButton ? 0.8f : 1;
            bool isThemed = interfaceTheme?.Value.IsThemed() == true;

            if (Background != null)
                Background.Alpha = isThemed ? 0 : 1;

            if (headerText != null)
                headerText.Alpha = isThemed ? 0 : 1;

            if (isThemed)
            {
                if (Section.UseSmallerSidebarButton)
                {
                    Height = 0;
                    Padding = default;
                    textIconContent.Alpha = 0;
                    selectionIndicator.Alpha = 0;
                    stableSelectionBar.Alpha = 0;
                    return;
                }

                Height = 46;
                Padding = new MarginPadding { Vertical = 2.5f };
                textIconContent.Alpha = 1;
                if (iconContainer != null)
                {
                    iconContainer.X = 30;
                    iconContainer.Margin = default;
                }

                selectionIndicator.FadeOut(FADE_DURATION, Easing.OutQuint);
                if (Selected)
                {
                    textIconContent.FadeColour(Color4.White, FADE_DURATION, Easing.OutQuint);
                    stableSelectionBar.FadeIn(FADE_DURATION, Easing.OutQuint);
                }
                else
                {
                    textIconContent.FadeColour(IsHovered ? Color4.White : OsuColour.Gray(0.65f), FADE_DURATION, Easing.OutQuint);
                    stableSelectionBar.FadeOut(FADE_DURATION, Easing.OutQuint);
                }

                return;
            }

            Height = 46 * scale;
            Padding = new MarginPadding { Horizontal = 5 + (Section.UseSmallerSidebarButton ? icon_size : 0), Vertical = 2.5f * scale };
            textIconContent.Alpha = 1;
            if (iconContainer != null)
            {
                iconContainer.X = icon_size;
                iconContainer.Margin = new MarginPadding { Left = 25 };
            }

            stableSelectionBar?.FadeOut(FADE_DURATION, Easing.OutQuint);

            if (Selected)
            {
                textIconContent.FadeColour(ColourProvider.Content1, FADE_DURATION, Easing.OutQuint);

                selectionIndicator.FadeIn(FADE_DURATION, Easing.OutQuint);
                selectionIndicator.ResizeHeightTo(selection_indicator_height_active, FADE_DURATION, Easing.OutElasticHalf);
            }
            else
            {
                textIconContent.FadeColour(IsHovered ? ColourProvider.Light1 : ColourProvider.Light3, FADE_DURATION, Easing.OutQuint);

                selectionIndicator.FadeOut(FADE_DURATION, Easing.OutQuint);
                selectionIndicator.ResizeHeightTo(selection_indicator_height_inactive, FADE_DURATION, Easing.OutQuint);
            }
        }
    }
}
