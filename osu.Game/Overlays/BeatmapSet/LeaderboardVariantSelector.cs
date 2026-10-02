// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Select;
using osuTK.Graphics;

namespace osu.Game.Overlays.BeatmapSet
{
    /// <summary>
    /// osu!somtum: switches the beatmap page's scoreboard between the vanilla, Relax and Autopilot leaderboards.
    /// </summary>
    public partial class LeaderboardVariantSelector : GradientLineTabControl<LeaderboardVariant>
    {
        protected override bool AddEnumEntriesAutomatically => false;

        protected override TabItem<LeaderboardVariant> CreateTabItem(LeaderboardVariant value) => new VariantTabItem(value);

        public LeaderboardVariantSelector()
        {
            AddItem(LeaderboardVariant.Vanilla);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            AccentColour = colourProvider.Highlight1;
            LineColour = colourProvider.Background1;
        }

        private partial class VariantTabItem : PageTabItem
        {
            public VariantTabItem(LeaderboardVariant value)
                : base(value)
            {
            }

            protected override LocalisableString CreateText()
            {
                switch (Value)
                {
                    case LeaderboardVariant.Relax:
                        return @"Relax";

                    case LeaderboardVariant.Autopilot:
                        return @"Autopilot";

                    default:
                        return @"Vanilla";
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                Text.FadeColour(AccentColour);

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);

                Text.FadeColour(Color4.White);
            }
        }
    }
}
