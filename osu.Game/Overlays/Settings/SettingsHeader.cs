// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings
{
    public partial class SettingsHeader : Container
    {
        private readonly LocalisableString heading;
        private readonly LocalisableString subheading;
        private IBindable<InterfaceTheme> interfaceTheme = null!;

        public SettingsHeader(LocalisableString heading, LocalisableString subheading)
        {
            this.heading = heading;
            this.subheading = subheading;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuConfigManager config)
        {
            interfaceTheme = config.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            OsuTextFlowContainer flow;
            Children = new Drawable[]
            {
                flow = new OsuTextFlowContainer
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Padding = new MarginPadding
                    {
                        Left = SettingsPanel.CONTENT_PADDING.Left,
                        Right = SettingsPanel.CONTENT_PADDING.Right,
                        Vertical = 20,
                    }
                }
            };

            interfaceTheme.BindValueChanged(t =>
            {
                bool isThemed = t.NewValue.IsThemed();
                flow.Clear();
                flow.TextAnchor = isThemed ? Anchor.TopCentre : Anchor.TopLeft;
                flow.AddText(heading, header =>
                {
                    header.Font = isThemed ? OsuFont.GetFont(size: 28, weight: FontWeight.Regular) : OsuFont.TorusAlternate.With(size: 40);
                    header.Colour = Color4.White;
                });
                flow.NewLine();
                flow.AddText(subheading, subheader =>
                {
                    subheader.Colour = isThemed ? Color4Extensions.FromHex(@"EB459E") : colourProvider.Content2;
                    subheader.Font = OsuFont.GetFont(size: isThemed ? 16 : 18);
                });
            }, true);
        }
    }
}
