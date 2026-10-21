// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings
{
    /// <summary>
    /// Potato Mode toggle for Settings -> Graphics -> Renderer. Potato Mode is
    /// an extreme-performance preset for weak PCs and phones; it's read once at
    /// startup (see <see cref="osu.Game.Performance.PotatoMode"/>) and
    /// gates heavy-visual subsystems at construction, so flipping it
    /// mid-run would leave a torn mix of cheap and expensive surfaces.
    /// Changing it therefore prompts for a restart.
    /// </summary>
    public partial class PotatoModeToggleAndRestart : Container
    {
        // Resolved optionally because the test-scene host doesn't register
        // OsuGame / IDialogOverlay. In that case the checkbox binds without
        // the restart prompt — the test can flip the setting freely.
        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            var potatoBindable = config.GetBindable<bool>(OsuSetting.PotatoMode);

            InternalChild = new SettingsItemV2(new FormCheckBox
            {
                Caption = "Potato mode (extreme performance)",
                HintText = "For weak PCs and phones. Strips almost all motion and effects: no animated triangles, "
                           + "no beat-synced pulsing anywhere (carousel cards, logo, menu buttons), no "
                           + "logo visualiser, no storyboards, no background blur, no hit lighting, no "
                           + "star fountains, no menu parallax, no seasonal backgrounds, and no live "
                           + "star-rating recalculation while scrolling song select. Fully dims "
                           + "gameplay backgrounds and switches to the legacy audio engine to avoid stutters. "
                           + "Frees up GPU, CPU and memory for max FPS. Your own graphics settings are left untouched. "
                           + "Changing this option restarts the game.",
                Current = potatoBindable,
            })
            {
                Keywords = new[] { @"potato", @"extreme", @"mode", @"performance", @"fps", @"low end", @"weak", @"lag", @"stutter", @"perf", @"quality", @"battery" },
            };

            PotatoModeRestartCoordinator.EnsureRegistered(potatoBindable, game, dialogOverlay);
        }
    }

    /// <summary>
    /// Process-wide one-shot subscription that owns the restart-confirm
    /// flow for <see cref="OsuSetting.PotatoMode"/>.
    /// </summary>
    internal static class PotatoModeRestartCoordinator
    {
        private static bool registered;
        private static readonly object register_lock = new object();

        private static bool suppress;

        public static void EnsureRegistered(Bindable<bool> potatoBindable, OsuGame? game, IDialogOverlay? dialogOverlay)
        {
            if (registered)
                return;

            lock (register_lock)
            {
                if (registered)
                    return;

                potatoBindable.BindValueChanged(change =>
                {
                    if (suppress || change.NewValue == change.OldValue)
                        return;

                    dialogOverlay?.Push(new ConfirmDialog(
                        "Potato mode changes a lot of visuals, so the game needs to restart. It will close now, please open it again.",
                        () => game?.AttemptExit(),
                        () =>
                        {
                            suppress = true;
                            potatoBindable.Value = change.OldValue;
                            suppress = false;
                        }));
                });

                registered = true;
            }
        }
    }
}
