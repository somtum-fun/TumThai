// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics.Colour;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osuTK.Graphics;

namespace osu.Game.Configuration
{
    /// <summary>
    /// TumThai: the look of the interface (HUD, song select listing and results screen).
    /// </summary>
    public enum InterfaceTheme
    {
        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.ThemeTumThai))]
        TumThai,

        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.ThemeOsuStable))]
        OsuStable,

        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.ThemeOsuDroid))]
        OsuDroid,
    }

    public static class InterfaceThemeExtensions
    {
        /// <summary>
        /// The theme that applies on the current platform. The osu!droid theme is Android-only.
        /// </summary>
        public static InterfaceTheme Effective(this InterfaceTheme theme)
            => theme == InterfaceTheme.OsuDroid && RuntimeInfo.OS != RuntimeInfo.Platform.Android ? InterfaceTheme.TumThai : theme;

        public static bool IsThemed(this InterfaceTheme theme) => theme.Effective() != InterfaceTheme.TumThai;

        public static ColourInfo DifficultyRowBackground(this InterfaceTheme theme, ColourInfo fallback) => theme.Effective() switch
        {
            InterfaceTheme.OsuStable => ColourInfo.GradientHorizontal(Color4Extensions.FromHex("0096EC"), Color4Extensions.FromHex("0072B5")),
            InterfaceTheme.OsuDroid => ColourInfo.GradientHorizontal(Color4Extensions.FromHex("1919F0"), Color4Extensions.FromHex("1212B0")),
            _ => fallback,
        };

        public static Color4 SetCardTint(this InterfaceTheme theme) => theme.Effective() switch
        {
            InterfaceTheme.OsuStable => Color4Extensions.FromHex("E96800"),
            InterfaceTheme.OsuDroid => Color4Extensions.FromHex("F09600"),
            _ => Color4.White,
        };

        public static Color4 GroupBackground(this InterfaceTheme theme, Color4 fallback) => theme.Effective() switch
        {
            InterfaceTheme.OsuStable => Color4Extensions.FromHex("23328F"),
            InterfaceTheme.OsuDroid => Color4Extensions.FromHex("1E1E2E"),
            _ => fallback,
        };

        public static Color4 ResultsBottomBar(this InterfaceTheme theme) => theme.Effective() switch
        {
            InterfaceTheme.OsuDroid => Color4Extensions.FromHex("1E1E2E"),
            _ => Color4Extensions.FromHex("333333"),
        };

        public static ColourInfo ResultsTopLayer(this InterfaceTheme theme) => theme.Effective() switch
        {
            InterfaceTheme.OsuStable => ColourInfo.GradientVertical(Color4Extensions.FromHex("3A4A63"), Color4Extensions.FromHex("2B374A")),
            InterfaceTheme.OsuDroid => ColourInfo.GradientVertical(Color4Extensions.FromHex("3E7DD3"), Color4Extensions.FromHex("2B5EA6")),
            _ => ColourInfo.GradientVertical(Color4Extensions.FromHex("444444"), Color4Extensions.FromHex("333333")),
        };

        public static ColourInfo ResultsMiddleLayer(this InterfaceTheme theme) => theme.Effective() switch
        {
            InterfaceTheme.OsuStable => ColourInfo.GradientVertical(Color4Extensions.FromHex("465772"), Color4Extensions.FromHex("243247")),
            InterfaceTheme.OsuDroid => ColourInfo.GradientVertical(Color4Extensions.FromHex("2B5EA6"), Color4Extensions.FromHex("234C86")),
            _ => ColourInfo.GradientVertical(Color4Extensions.FromHex("555555"), Color4Extensions.FromHex("333333")),
        };

        public static Color4 ResultsContractedLayer(this InterfaceTheme theme) => theme.Effective() switch
        {
            InterfaceTheme.OsuStable => Color4Extensions.FromHex("2B374A"),
            InterfaceTheme.OsuDroid => Color4Extensions.FromHex("2B5EA6"),
            _ => Color4Extensions.FromHex("353535"),
        };
    }
}
