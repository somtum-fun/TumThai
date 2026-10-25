// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Settings.Sections.General
{
    /// <summary>
    /// TumThai: the custom rulesets the server ranks (from rulesets.info, a file pinned by staff for each), to install,
    /// update or remove. A ruleset put in the rulesets folder by hand works too, but submits only when it's the pinned file.
    /// Changes take effect when TumThai starts again.
    /// </summary>
    public partial class CustomRulesetsSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Custom rulesets";

        [Resolved]
        private SomtumCustomRulesets customRulesets { get; set; } = null!;

        private FillFlowContainer list = null!;
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
            Add(list = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new osuTK.Vector2(0, SettingsSection.ITEM_SPACING_V2),
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            customRulesets.Available.BindCollectionChanged((_, _) => rebuild(), true);
        }

        private void rebuild()
        {
            list.Clear();

            if (customRulesets.Available.Count == 0)
            {
                setNote(@"Custom rulesets the server ranks show here once you're signed in. Their scores go on their own leaderboards, by score.", SettingsNote.Type.Informational);
                return;
            }

            bool restart = false;

            foreach (var entry in customRulesets.Available.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                var state = customRulesets.StateOf(entry);
                restart |= state is CustomRulesetState.RestartToLoad or CustomRulesetState.RestartToUpdate or CustomRulesetState.RestartToRemove;
                string version = entry.Version is { Length: > 0 } v ? $@" {v}" : string.Empty;

                switch (state)
                {
                    case CustomRulesetState.NotInstalled:
                        list.Add(button($@"Install {entry.Name}{version}", entry));
                        break;

                    case CustomRulesetState.UpdateAvailable:
                        list.Add(button($@"Update {entry.Name} to{version}", entry));
                        break;

                    case CustomRulesetState.Installed:
                        list.Add(removeButton($@"{entry.Name}{version}: installed (remove)", entry));
                        break;

                    case CustomRulesetState.RestartToLoad:
                    case CustomRulesetState.RestartToUpdate:
                        list.Add(new SettingsButtonV2 { Text = $@"{entry.Name}{version}: ready, restart TumThai to load it", Enabled = { Value = false } });
                        break;

                    case CustomRulesetState.RestartToRemove:
                        list.Add(new SettingsButtonV2 { Text = $@"{entry.Name}: removed when TumThai restarts", Enabled = { Value = false } });
                        break;
                }
            }

            setNote(restart
                ? @"Restart TumThai to finish installing, updating or removing rulesets."
                : @"Rulesets from rulesets.info that this server ranks, by score. A play submits only on the version shown here: update a ruleset when it says so.",
                restart ? SettingsNote.Type.Warning : SettingsNote.Type.Informational);
        }

        private SettingsButtonV2 button(string text, APISomtumCustomRuleset entry)
        {
            var b = new SettingsButtonV2 { Text = text };
            b.Action = () =>
            {
                b.Enabled.Value = false;
                b.Text = $@"Downloading {entry.Name}…";
                Task.Run(() => customRulesets.InstallAsync(entry)).ContinueWith(t => Schedule(() =>
                {
                    if (t.IsFaulted)
                    {
                        Logger.Error(t.Exception, $@"Couldn't install the {entry.Name} ruleset");
                        setNote($@"Couldn't install {entry.Name}: {t.Exception?.GetBaseException().Message}", SettingsNote.Type.Critical);
                        b.Enabled.Value = true;
                        b.Text = text;
                        return;
                    }

                    rebuild();
                }));
            };
            return b;
        }

        private SettingsButtonV2 removeButton(string text, APISomtumCustomRuleset entry) => new DangerousSettingsButtonV2
        {
            Text = text,
            Action = () =>
            {
                try
                {
                    customRulesets.Remove(entry);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Couldn't remove the {entry.Name} ruleset");
                    setNote($@"Couldn't remove {entry.Name}: {e.Message}", SettingsNote.Type.Critical);
                    return;
                }

                rebuild();
            },
        };

        private void setNote(string text, SettingsNote.Type type) => note.Current.Value = new SettingsNote.Data(text, type);
    }
}
