// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Input
{
    /// <summary>
    /// TumThai: whether this device has a touch screen, as far as the game can tell: a finger has touched it this session (a
    /// stylus doesn't count). Unlike <see cref="Configuration.Static.TouchInputActive"/> it stays set when the mouse is used again,
    /// so touch-only modes (osu!droid) stay playable on a touch screen laptop.
    /// </summary>
    public static class TouchScreen
    {
        public static bool Detected { get; internal set; }
    }
}
