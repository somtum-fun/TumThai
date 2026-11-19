// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// TumThai: a mod only some devices can play (osu!droid: phones and tablets).
    /// On other devices its <see cref="Mod.UserPlayable"/> is false, but it's still offered in mod select so leaderboards can be
    /// filtered by it. Song select won't start a play with it there.
    /// </summary>
    public interface IDeviceOnlyMod
    {
        /// <summary>
        /// Why the mod can't be played on this device, e.g. "osu!droid can only be played on a phone or tablet."
        /// </summary>
        LocalisableString NotPlayableHere { get; }
    }
}
