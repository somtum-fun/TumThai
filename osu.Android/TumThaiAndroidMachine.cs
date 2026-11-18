// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Android.App;
using Android.Provider;
using osu.Game.Online;

namespace osu.Android
{
    /// <summary>
    /// Android's machine details for <see cref="TumThaiClient"/>: <c>ANDROID_ID</c>, which stays the same for
    /// TumThai on this device and user until a factory reset. Android has no osu!(stable).
    /// </summary>
    public class TumThaiAndroidMachine : ITumThaiMachineInfo
    {
        public string? StableUninstallId => null;

        public string? MachineId => Settings.Secure.GetString(Application.Context.ContentResolver, Settings.Secure.AndroidId);
    }
}
