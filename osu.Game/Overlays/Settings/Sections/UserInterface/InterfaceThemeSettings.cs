// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class InterfaceThemeSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.InterfaceThemeHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            InterfaceTheme[] items = RuntimeInfo.OS == RuntimeInfo.Platform.Android
                ? Enum.GetValues<InterfaceTheme>()
                : new[] { InterfaceTheme.TumThai, InterfaceTheme.OsuStable };

            var themeBindable = config.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);

            if (RuntimeInfo.OS != RuntimeInfo.Platform.Android && themeBindable.Value == InterfaceTheme.OsuDroid)
                themeBindable.Value = InterfaceTheme.TumThai;

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<InterfaceTheme>
                {
                    Caption = UserInterfaceStrings.InterfaceThemeHeader,
                    HintText = UserInterfaceStrings.InterfaceThemeDescription,
                    Items = items,
                    Current = themeBindable,
                })
                {
                    Keywords = new[] { "theme", "interface", "ui", "stable", "droid", "skin" },
                },
            };
        }
    }
}
