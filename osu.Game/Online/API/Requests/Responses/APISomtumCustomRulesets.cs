// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Responses
{
    /// <summary>TumThai: the server's custom rulesets (<see cref="GetSomtumCustomRulesetsRequest"/>).</summary>
    public class APISomtumCustomRulesets
    {
        [JsonProperty("rulesets")]
        public List<APISomtumCustomRuleset> Rulesets { get; set; } = new List<APISomtumCustomRuleset>();
    }

    /// <summary>
    /// TumThai: one custom ruleset the server ranks: the id its plays submit with, and the file (by URL and SHA-256)
    /// they have to be played on.
    /// </summary>
    public class APISomtumCustomRuleset
    {
        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>The DLL's assembly name, e.g. <c>osu.Game.Rulesets.Sentakki</c>.</summary>
        [JsonProperty("assembly")]
        public string Assembly { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("version")]
        public string? Version { get; set; }

        [JsonProperty("download_url")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonProperty("sha256")]
        public string Sha256 { get; set; } = string.Empty;

        [JsonProperty("size")]
        public long Size { get; set; }
    }
}
