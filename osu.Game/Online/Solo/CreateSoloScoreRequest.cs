// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;

namespace osu.Game.Online.Solo
{
    public class CreateSoloScoreRequest : APIRequest<APIScoreToken>
    {
        private readonly BeatmapInfo beatmapInfo;
        private readonly int rulesetId;
        private readonly string versionHash;

        // TumThai: the play's mods, sent as "mods" (acronyms, comma separated): the server needs them before the
        // play starts for osu!dan maps, which are submitted only with Classic (CL).
        private readonly IEnumerable<string>? modAcronyms;

        public CreateSoloScoreRequest(BeatmapInfo beatmapInfo, int rulesetId, string versionHash, IEnumerable<string>? modAcronyms = null)
        {
            this.beatmapInfo = beatmapInfo;
            this.rulesetId = rulesetId;
            this.versionHash = versionHash;
            this.modAcronyms = modAcronyms;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Post;
            req.AddParameter("version_hash", versionHash);
            req.AddParameter("beatmap_hash", beatmapInfo.MD5Hash);
            req.AddParameter("ruleset_id", rulesetId.ToString(CultureInfo.InvariantCulture));
            if (modAcronyms != null)
                req.AddParameter("mods", string.Join(',', modAcronyms.Where(a => !string.IsNullOrEmpty(a))));
            return req;
        }

        protected override string Target => $@"beatmaps/{beatmapInfo.OnlineID}/solo/scores";
    }
}
