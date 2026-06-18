// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Sections.Server;

namespace osu.Game.Overlays.Settings.Sections
{
    public partial class ServerSection : SettingsSection
    {
        public override LocalisableString Header => OnlineSettingsStrings.ServerSectionHeader;

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.Global
        };

        public ServerSection()
        {
            Children = new Drawable[]
            {
                new ServerSettings(),
            };
        }
    }
}
