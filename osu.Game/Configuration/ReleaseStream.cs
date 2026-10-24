// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Configuration
{
    // TumThai: TumThai's two streams. The names stay ppy's (they're what the settings file keeps).
    public enum ReleaseStream
    {
        [Description("TumThai")]
        Lazer,

        /// <summary>TumThai!Next: the same game on tumthai-framework (versions ending in -next, Velopack channel win-next).</summary>
        [Description("TumThai!Next")]
        Tachyon
    }
}
