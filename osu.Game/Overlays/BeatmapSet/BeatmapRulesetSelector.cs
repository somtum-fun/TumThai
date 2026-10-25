// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Rulesets;
using System.Linq;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.BeatmapSet
{
    public partial class BeatmapRulesetSelector : OverlayRulesetSelector
    {
        private readonly Bindable<APIBeatmapSet?> beatmapSet = new Bindable<APIBeatmapSet?>();

        // TumThai: custom rulesets play a set's osu! difficulties (the tab's on when it has some), with boards of their own.
        protected override bool LegacyOnly => false;

        public APIBeatmapSet? BeatmapSet
        {
            get => beatmapSet.Value;
            set
            {
                // propagate value to tab items first to enable only available rulesets.
                beatmapSet.Value = value;

                Current.Value = TabContainer.TabItems.FirstOrDefault(t => t.Enabled.Value)?.Value;
            }
        }

        protected override TabItem<RulesetInfo> CreateTabItem(RulesetInfo value) => new BeatmapRulesetTabItem(value, this)
        {
            BeatmapSet = { BindTarget = beatmapSet }
        };
    }
}
