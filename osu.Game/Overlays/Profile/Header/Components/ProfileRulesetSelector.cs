// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Extensions;
using osu.Game.Rulesets;

namespace osu.Game.Overlays.Profile.Header.Components
{
    public partial class ProfileRulesetSelector : OverlayRulesetSelector
    {
        [Resolved]
        private UserProfileOverlay? profileOverlay { get; set; }

        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        // TumThai: custom rulesets have profiles too (their stats and plays on the server), so they're tabs here.
        protected override bool LegacyOnly => false;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            User.BindValueChanged(user => updateState(user.NewValue), true);
            Current.BindValueChanged(ruleset =>
            {
                if (User.Value != null && !ruleset.NewValue.Equals(User.Value.Ruleset))
                    profileOverlay?.ShowUser(User.Value.User, ruleset.NewValue);
            });
        }

        private List<RulesetInfo> getItemsWithSpecialRulesets()
        {
            var baseItems = Items.ToList();

            foreach (var ruleset in Rulesets.AvailableRulesets)
            {
                switch (ruleset.ShortName)
                {
                    case RulesetInfo.OSU_MODE_SHORTNAME:
                        baseItems.Add(ruleset.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID));
                        baseItems.Add(ruleset.CreateSpecialRuleset(RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME, RulesetInfo.OSU_AUTOPILOT_ONLINE_ID));
                        break;

                    case RulesetInfo.TAIKO_MODE_SHORTNAME:
                        baseItems.Add(ruleset.CreateSpecialRuleset(RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME, RulesetInfo.TAIKO_RELAX_ONLINE_ID));
                        break;

                    case RulesetInfo.CATCH_MODE_SHORTNAME:
                        baseItems.Add(ruleset.CreateSpecialRuleset(RulesetInfo.CATCH_RELAX_MODE_SHORTNAME, RulesetInfo.CATCH_RELAX_ONLINE_ID));
                        break;
                }
            }

            return baseItems;
        }

        private void updateState(UserProfileData? user)
        {
            // TumThai: custom rulesets have no online ID, so they're matched by short name.
            Current.Value = getItemsWithSpecialRulesets().FirstOrDefault(ruleset => user?.Ruleset.MatchesOnlineID(ruleset) == true
                                                                                   || (user != null && !ruleset.IsLegacyRuleset() && !ruleset.IsSpecialRuleset()
                                                                                       && ruleset.ShortName == user.Ruleset.ShortName));
            SetDefaultRuleset(Rulesets.GetRuleset(user?.User.PlayMode ?? @"osu").AsNonNull());
        }

        public void SetDefaultRuleset(RulesetInfo ruleset)
        {
            foreach (var tabItem in TabContainer)
                ((ProfileRulesetTabItem)tabItem).IsDefault = ((ProfileRulesetTabItem)tabItem).Value.Equals(ruleset);
        }

        protected override TabItem<RulesetInfo> CreateTabItem(RulesetInfo value) => new ProfileRulesetTabItem(value, this);
    }
}
