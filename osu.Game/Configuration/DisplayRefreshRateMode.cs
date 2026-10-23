// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Configuration
{
    public enum DisplayRefreshRateMode
    {
        [Description("Automatic (Highest)")]
        Automatic = 0,

        [Description("60 Hz")]
        Hz60 = 60,

        [Description("90 Hz")]
        Hz90 = 90,

        [Description("120 Hz")]
        Hz120 = 120,

        [Description("144 Hz")]
        Hz144 = 144,

        [Description("165 Hz")]
        Hz165 = 165,

        [Description("240 Hz")]
        Hz240 = 240,
    }
}
