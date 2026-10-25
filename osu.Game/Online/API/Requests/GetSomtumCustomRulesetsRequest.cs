// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// TumThai: the custom rulesets the server ranks, with their pinned files (<c>GET /api/v2/tomyum/rulesets</c>).
    /// </summary>
    public class GetSomtumCustomRulesetsRequest : APIRequest<APISomtumCustomRulesets>
    {
        protected override string Target => @"tomyum/rulesets";
    }
}
