// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class InterfaceThemeSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.InterfaceThemeHeader;

        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            InterfaceTheme[] items = Enum.GetValues<InterfaceTheme>();

            var themeBindable = config.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);

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

            InterfaceThemeRestartCoordinator.EnsureRegistered(themeBindable, game, dialogOverlay, config);
        }
    }

    /// <summary>
    /// Prompts the user to restart/reload the game when <see cref="OsuSetting.InterfaceTheme"/> is changed
    /// so that all screens, overlays, and skin caches initialize cleanly under the selected theme without overlap.
    /// </summary>
    internal static class InterfaceThemeRestartCoordinator
    {
        private static bool registered;
        private static readonly object register_lock = new object();
        private static bool suppress;

        public static void EnsureRegistered(Bindable<InterfaceTheme> themeBindable, OsuGame? game, IDialogOverlay? dialogOverlay, OsuConfigManager config)
        {
            if (registered || dialogOverlay == null)
                return;

            lock (register_lock)
            {
                if (registered)
                    return;

                themeBindable.BindValueChanged(change =>
                {
                    if (suppress || change.NewValue == change.OldValue)
                        return;

                    config.Save();

                    dialogOverlay.Push(new ConfirmDialog(
                        "Switching the interface theme requires restarting the game so all screens and skin caches reload cleanly without overlap. Restart now?",
                        () =>
                        {
                            config.Save();
                            game?.RestartAppWhenExited();
                            game?.AttemptExit();
                        },
                        () =>
                        {
                            suppress = true;
                            themeBindable.Value = change.OldValue;
                            config.Save();
                            suppress = false;
                        }));
                });

                registered = true;
            }
        }
    }
}
