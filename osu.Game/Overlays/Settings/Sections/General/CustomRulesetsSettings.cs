// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Online;

namespace osu.Game.Overlays.Settings.Sections.General
{
    /// <summary>
    /// TumThai: the custom rulesets the server ranks (from rulesets.info, a file pinned by staff for each): a line saying
    /// where they stand and a button for the window that installs, updates and removes them
    /// (<see cref="SomtumCustomRulesetsDialog"/>). A ruleset put in the rulesets folder by hand works too, but submits only
    /// when it's the pinned file.
    /// </summary>
    public partial class CustomRulesetsSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Custom rulesets";

        [Resolved]
        private SomtumCustomRulesets customRulesets { get; set; } = null!;

        [Resolved]
        private SomtumCustomRulesetsDialog? dialog { get; set; }

        private readonly SettingsNote note = new SettingsNote { RelativeSizeAxes = Axes.X };

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
                Child = note,
            });
            Add(new SettingsButtonV2
            {
                Text = @"Install custom rulesets…",
                Action = () => dialog?.Show(),
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            customRulesets.Available.BindCollectionChanged((_, _) => updateNote(), true);
        }

        private void updateNote()
        {
            int count = customRulesets.Available.Count;

            if (count == 0)
            {
                note.Current.Value = new SettingsNote.Data(@"Custom rulesets the server ranks show here once you're signed in. Their scores go on their own leaderboards, by score.",
                    SettingsNote.Type.Informational);
                return;
            }

            var states = customRulesets.Available.Select(customRulesets.StateOf).ToList();
            int installed = states.Count(s => s is CustomRulesetState.Installed);
            int updates = states.Count(s => s is CustomRulesetState.UpdateAvailable);
            int restart = states.Count(s => s is CustomRulesetState.RestartToLoad or CustomRulesetState.RestartToUpdate or CustomRulesetState.RestartToRemove);

            string text = $"This server ranks {count} custom ruleset{(count == 1 ? string.Empty : "s")}; {installed} installed here.";
            if (updates > 0)
                text += $" {updates} need{(updates == 1 ? "s" : string.Empty)} an update.";
            if (restart > 0)
                text += " Restart TumThai to finish a change.";

            note.Current.Value = new SettingsNote.Data(text, updates > 0 || restart > 0 ? SettingsNote.Type.Warning : SettingsNote.Type.Informational);
        }
    }
}
