// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    /// <summary>
    /// TumThai: which results screen solo plays show.
    /// </summary>
    public enum ResultsScreenStyle
    {
        /// <summary>The interface theme's: osu!stable's for the osu!stable theme, lazer's otherwise.</summary>
        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.ResultsStyleAuto))]
        Auto,

        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.ResultsStyleLazer))]
        Lazer,

        /// <summary>A big score and rank ring (the design ported from torii, MIT).</summary>
        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.ResultsStyleV2))]
        V2,

        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.ResultsStyleStable))]
        Stable,

        /// <summary>The leaderboard, the beatmap inside the accuracy ring, and the numbers beside it.</summary>
        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.ResultsStyleSpotlight))]
        Spotlight,

        /// <summary>ppy's Rev 11 results design, as drawn.</summary>
        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.ResultsStyleRev11))]
        Rev11,
    }
}
