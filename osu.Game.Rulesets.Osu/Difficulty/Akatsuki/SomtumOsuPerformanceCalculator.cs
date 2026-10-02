// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Relax;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki
{
    /// <summary>
    /// osu!somtum: ppy's pp, except for Akatsuki's difficulty (Relax and Autopilot plays, from
    /// <see cref="SomtumOsuDifficultyCalculator"/>), which gets Akatsuki's pp.
    /// </summary>
    public class SomtumOsuPerformanceCalculator : OsuPerformanceCalculator
    {
        private readonly AkatsukiRelaxPerformanceCalculator relax = new AkatsukiRelaxPerformanceCalculator();
        private readonly AkatsukiAutopilotPerformanceCalculator autopilot = new AkatsukiAutopilotPerformanceCalculator();

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            switch (attributes)
            {
                case AkatsukiRelaxDifficultyAttributes:
                    return relax.Calculate(score, attributes);

                case AkatsukiAutopilotDifficultyAttributes:
                    return autopilot.Calculate(score, attributes);

                default:
                    return base.CreatePerformanceAttributes(score, attributes);
            }
        }
    }
}
