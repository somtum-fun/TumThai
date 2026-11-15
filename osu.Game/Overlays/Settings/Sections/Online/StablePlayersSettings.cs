// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Overlays.Settings.Sections.Online
{
    /// <summary>
    /// TumThai (experimental): osu!(stable) players in the online list and friends, and spectating them, on servers
    /// that have them (TomYum's spectator server, with its tomyum.stable-spectate plugin).
    /// </summary>
    public partial class StablePlayersSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "osu!(stable) players";

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Show osu!(stable) players (experimental)",
                    HintText = "See who's playing on osu!(stable) in Currently online and your friends, and spectate them. "
                               + "While you watch, the server's bot joins them as a spectator in osu!(stable).",
                    Current = config.GetBindable<bool>(OsuSetting.ShowStablePlayers),
                }),
            };
        }
    }
}
