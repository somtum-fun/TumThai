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

        /// <summary>
        /// The osu!droid theme, removed. Kept only so a setting saved with it still loads; it's treated as <see cref="OsuStable"/>.
        /// </summary>
        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.ThemeOsuDroid))]
        OsuDroid,
    }

    public static class InterfaceThemeExtensions
    {
        /// <summary>
        /// The theme that applies on the current platform.
        /// </summary>
        public static InterfaceTheme Effective(this InterfaceTheme theme) => theme == InterfaceTheme.OsuDroid ? InterfaceTheme.OsuStable : theme;

        public static bool IsThemed(this InterfaceTheme theme) => theme.Effective() != InterfaceTheme.TumThai;

        public static ColourInfo DifficultyRowBackground(this InterfaceTheme theme, ColourInfo fallback) => theme.Effective() switch
        {
            InterfaceTheme.OsuStable => ColourInfo.GradientHorizontal(Color4Extensions.FromHex("0096EC"), Color4Extensions.FromHex("0072B5")),
            _ => fallback,
        };

        public static Color4 SetCardTint(this InterfaceTheme theme) => theme.Effective() switch
        {
            InterfaceTheme.OsuStable => Color4Extensions.FromHex("E96800"),
            _ => Color4.White,
        };

        public static Color4 GroupBackground(this InterfaceTheme theme, Color4 fallback) => theme.Effective() switch
        {
            InterfaceTheme.OsuStable => Color4Extensions.FromHex("23328F"),
            _ => fallback,
        };

        public static Color4 ResultsBottomBar(this InterfaceTheme theme) => theme.Effective() switch
        {
            _ => Color4Extensions.FromHex("333333"),
        };

        public static ColourInfo ResultsTopLayer(this InterfaceTheme theme) => theme.Effective() switch
        {
            InterfaceTheme.OsuStable => ColourInfo.GradientVertical(Color4Extensions.FromHex("3A4A63"), Color4Extensions.FromHex("2B374A")),
            _ => ColourInfo.GradientVertical(Color4Extensions.FromHex("444444"), Color4Extensions.FromHex("333333")),
        };

        public static ColourInfo ResultsMiddleLayer(this InterfaceTheme theme) => theme.Effective() switch
        {
            InterfaceTheme.OsuStable => ColourInfo.GradientVertical(Color4Extensions.FromHex("465772"), Color4Extensions.FromHex("243247")),
            _ => ColourInfo.GradientVertical(Color4Extensions.FromHex("555555"), Color4Extensions.FromHex("333333")),
        };

        public static Color4 ResultsContractedLayer(this InterfaceTheme theme) => theme.Effective() switch
        {
            InterfaceTheme.OsuStable => Color4Extensions.FromHex("2B374A"),
            _ => Color4Extensions.FromHex("353535"),
        };
    }
}
