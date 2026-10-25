// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Users;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// TumThai: the signed-in player's standing in a custom ruleset (<c>GET /api/v2/tomyum/rulesets/{slug}/me</c>): global and
    /// country rank by ranked score, and the ranked score, named as a user's statistics are.
    /// </summary>
    public class GetSomtumCustomRulesetStandingRequest : APIRequest<UserStatistics>
    {
        private readonly string slug;

        public GetSomtumCustomRulesetStandingRequest(string slug)
        {
            this.slug = slug;
        }

        protected override string Target => $@"tomyum/rulesets/{slug}/me";
    }
}
