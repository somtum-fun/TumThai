// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Relax;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki
{
    /// <summary>
    /// osu!somtum: ppy's difficulty, except Relax and Autopilot plays, which get Akatsuki's (as the
    /// server works out their pp), so star ratings and the in-game pp counter match their leaderboards.
    /// </summary>
    public class SomtumOsuDifficultyCalculator : OsuDifficultyCalculator
    {
        private readonly AkatsukiRelaxDifficultyCalculator relax;
        private readonly AkatsukiAutopilotDifficultyCalculator autopilot;

        public SomtumOsuDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
            relax = new AkatsukiRelaxDifficultyCalculator(ruleset, beatmap);
            autopilot = new AkatsukiAutopilotDifficultyCalculator(ruleset, beatmap);
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            if (isRelax(mods))
                return relax.HitObjectsFor(beatmap, mods);
            if (isAutopilot(mods))
                return autopilot.HitObjectsFor(beatmap, mods);

            return base.CreateDifficultyHitObjects(beatmap, mods);
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            if (isRelax(mods))
                return relax.SkillsFor(beatmap, mods);
            if (isAutopilot(mods))
                return autopilot.SkillsFor(beatmap, mods);

            return base.CreateSkills(beatmap, mods);
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (isRelax(mods))
                return relax.AttributesFor(beatmap, mods, skills);
            if (isAutopilot(mods))
                return autopilot.AttributesFor(beatmap, mods, skills);

            return base.CreateDifficultyAttributes(beatmap, mods, skills);
        }

        private static bool isRelax(Mod[] mods) => mods.Any(m => m is OsuModRelax);

        private static bool isAutopilot(Mod[] mods) => mods.Any(m => m is OsuModAutopilot);
    }
}
