// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Extensions;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// A play variant of a legacy ruleset, used to switch a leaderboard between vanilla and the
    /// Relax / Autopilot "special" rulesets exposed by the g0v0 server.
    /// </summary>
    public enum LeaderboardVariant
    {
        Vanilla,
        Relax,
        Autopilot,
    }

    public static class LeaderboardVariantExtensions
    {
        /// <summary>
        /// Returns the special ruleset (e.g. <c>osurx</c>) for the given base ruleset and variant.
        /// For <see cref="LeaderboardVariant.Vanilla"/> or unsupported combinations the base ruleset is returned unchanged.
        /// </summary>
        public static RulesetInfo ApplyVariant(this RulesetInfo baseRuleset, LeaderboardVariant variant)
        {
            if (variant == LeaderboardVariant.Vanilla || !baseRuleset.HasSpecialRuleset())
                return baseRuleset;

            switch (baseRuleset.ShortName)
            {
                case RulesetInfo.OSU_MODE_SHORTNAME:
                    if (variant == LeaderboardVariant.Relax)
                        return baseRuleset.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID);
                    if (variant == LeaderboardVariant.Autopilot)
                        return baseRuleset.CreateSpecialRuleset(RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME, RulesetInfo.OSU_AUTOPILOT_ONLINE_ID);
                    break;

                case RulesetInfo.TAIKO_MODE_SHORTNAME:
                    if (variant == LeaderboardVariant.Relax)
                        return baseRuleset.CreateSpecialRuleset(RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME, RulesetInfo.TAIKO_RELAX_ONLINE_ID);
                    break;

                case RulesetInfo.CATCH_MODE_SHORTNAME:
                    if (variant == LeaderboardVariant.Relax)
                        return baseRuleset.CreateSpecialRuleset(RulesetInfo.CATCH_RELAX_MODE_SHORTNAME, RulesetInfo.CATCH_RELAX_ONLINE_ID);
                    break;
            }

            return baseRuleset;
        }

        /// <summary>
        /// Returns the variants supported by the given base ruleset. Always includes <see cref="LeaderboardVariant.Vanilla"/>.
        /// osu! supports Relax + Autopilot, osu!taiko / osu!catch support Relax, osu!mania supports neither.
        /// </summary>
        public static LeaderboardVariant[] SupportedVariants(this IRulesetInfo? baseRuleset)
        {
            switch (baseRuleset?.ShortName)
            {
                case RulesetInfo.OSU_MODE_SHORTNAME:
                    return new[] { LeaderboardVariant.Vanilla, LeaderboardVariant.Relax, LeaderboardVariant.Autopilot };

                case RulesetInfo.TAIKO_MODE_SHORTNAME:
                case RulesetInfo.CATCH_MODE_SHORTNAME:
                    return new[] { LeaderboardVariant.Vanilla, LeaderboardVariant.Relax };

                default:
                    return new[] { LeaderboardVariant.Vanilla };
            }
        }

        /// <summary>
        /// osu!somtum: the variant a play with these mods counts in: Autopilot with AP, Relax with RX, else vanilla.
        /// </summary>
        public static LeaderboardVariant VariantFor(IEnumerable<IMod> mods)
        {
            var acronyms = mods.Select(m => m.Acronym).ToList();

            if (acronyms.Contains("AP"))
                return LeaderboardVariant.Autopilot;

            return acronyms.Contains("RX") ? LeaderboardVariant.Relax : LeaderboardVariant.Vanilla;
        }

        /// <summary>
        /// osu!somtum: the ruleset whose leaderboard and statistics a play with these mods belongs to
        /// (e.g. <c>osurx</c> for osu! with RX); the base ruleset for vanilla plays and unsupported combinations.
        /// </summary>
        public static RulesetInfo ForMods(this RulesetInfo baseRuleset, IEnumerable<IMod> mods)
            => baseRuleset.ApplyVariant(VariantFor(mods));
    }
}
