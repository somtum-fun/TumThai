// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Online.Solo;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;

namespace osu.Game.Screens.Play
{
    public partial class SoloPlayer : SubmittingPlayer
    {
        // TumThai: the custom rulesets the server ranks.
        [Resolved(canBeNull: true)]
        [CanBeNull]
        private SomtumCustomRulesets customRulesets { get; set; }

        [Cached(typeof(IGameplayLeaderboardProvider))]
        private readonly SoloGameplayLeaderboardProvider leaderboardProvider = new SoloGameplayLeaderboardProvider { AllowPerformanceSort = true }; // osu!somtum

        public SoloPlayer([CanBeNull] PlayerConfiguration configuration = null)
            : base(configuration)
        {
            Configuration.ShowLeaderboard = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(leaderboardProvider);
        }

        protected override APIRequest<APIScoreToken> CreateTokenRequest()
        {
            int beatmapId = Beatmap.Value.BeatmapInfo.OnlineID;
            int rulesetId = Ruleset.Value.OnlineID;

            if (beatmapId <= 0)
                return null;

            if (Beatmap.Value.BeatmapInfo.Status == BeatmapOnlineStatus.LocallyModified)
                return null;

            if (!Ruleset.Value.IsLegacyRuleset())
            {
                // TumThai: a custom ruleset the server ranks submits under the server's id, played on the file it pinned.
                if (customRulesets?.Submittable(Ruleset.Value) is not { } custom)
                    return null;

                return new CreateSoloScoreRequest(Beatmap.Value.BeatmapInfo, custom.ID, Game.VersionHash, Mods.Value.Select(m => m.Acronym), custom.Sha256);
            }

            return new CreateSoloScoreRequest(Beatmap.Value.BeatmapInfo, rulesetId, Game.VersionHash, Mods.Value.Select(m => m.Acronym));
        }

        protected override bool ShouldExitOnTokenRetrievalFailure(Exception exception) => false;

        protected override APIRequest<MultiplayerScore> CreateSubmissionRequest(Score score, long token)
        {
            IBeatmapInfo beatmap = score.ScoreInfo.BeatmapInfo!;

            Debug.Assert(beatmap.OnlineID > 0);

            return new SubmitSoloScoreRequest(score.ScoreInfo, token, beatmap.OnlineID);
        }
    }
}
