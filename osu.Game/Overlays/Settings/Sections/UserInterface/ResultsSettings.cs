// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    /// <summary>
    /// TumThai: which results screen solo plays show (<see cref="ResultsScreenStyle"/>).
    /// </summary>
    public partial class ResultsSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.ResultsHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsEnumDropdown<ResultsScreenStyle>
                {
                    LabelText = UserInterfaceStrings.ResultsScreenStyle,
                    TooltipText = UserInterfaceStrings.ResultsScreenStyleDescription,
                    Current = config.GetBindable<ResultsScreenStyle>(OsuSetting.ResultsScreenStyle),
                    Keywords = new[] { @"results", @"ranking", @"score", @"modern", @"stable", @"layout" },
                },
            };
        }
    }
}
