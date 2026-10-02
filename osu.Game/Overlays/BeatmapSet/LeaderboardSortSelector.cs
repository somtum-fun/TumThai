// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.Leaderboards;
using osuTK.Graphics;

namespace osu.Game.Overlays.BeatmapSet
{
    /// <summary>
    /// osu!somtum: orders the beatmap page's scoreboard by pp or by score.
    /// </summary>
    public partial class LeaderboardSortSelector : GradientLineTabControl<LeaderboardSortMode>
    {
        protected override bool AddEnumEntriesAutomatically => false;

        protected override TabItem<LeaderboardSortMode> CreateTabItem(LeaderboardSortMode value) => new SortTabItem(value);

        public LeaderboardSortSelector()
        {
            AddItem(LeaderboardSortMode.PerformancePoints);
            AddItem(LeaderboardSortMode.Score);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            AccentColour = colourProvider.Highlight1;
            LineColour = colourProvider.Background1;
        }

        private partial class SortTabItem : PageTabItem
        {
            public SortTabItem(LeaderboardSortMode value)
                : base(value)
            {
            }

            protected override LocalisableString CreateText() => Value == LeaderboardSortMode.PerformancePoints ? @"By pp" : @"By score";

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
