// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.Settings.Sections.DebugSettings;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings.Sections
{
    public partial class DebugSection : SettingsSection
    {
        public override LocalisableString Header => @"Debug";

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.Debug
        };

        public DebugSection()
        {
            if (DebugUtils.IsDebugBuild)
            {
                Add(new GeneralSettings());
                Add(new BatchImportSettings());
            }

            Add(new MemorySettings());
            Add(new TumThaiNextBuildSubsection());
        }

        private partial class TumThaiNextBuildSubsection : SettingsSubsection
        {
            protected override LocalisableString Header => @"TumThai!Next";

            public TumThaiNextBuildSubsection()
            {
                Add(new OsuTextFlowContainer(t =>
                {
                    t.Font = OsuFont.GetFont(size: 14);
                    t.Colour = new Color4(210, 215, 222, 255);
                })
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Text = "You are running TumThai!Next. While TumThai and TumThai!Next share the same base, they are not the same build: TumThai!Next includes heavy framework, audio, input, and rendering modifications (powered by tumthai-framework) for lower latency and higher performance.",
                });
            }
        }
    }
}
