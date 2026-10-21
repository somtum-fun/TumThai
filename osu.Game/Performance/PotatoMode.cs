// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Performance
{
    /// <summary>
    /// Process-wide "Potato Mode" flag for low-end machines. Set ONCE at
    /// app startup by <see cref="SetFromConfig"/> from
    /// <see cref="osu.Game.Configuration.OsuSetting.PotatoMode"/>,
    /// BEFORE the visual tree builds. Read by the heavy-visual subsystems
    /// (storyboards, background blur + dim, menu parallax, kiai star
    /// fountains, seasonal backgrounds, hit lighting, song-select live star
    /// rating recalculation) to skip their expensive paths.
    ///
    /// Deliberately read-once and restart-gated rather than live-bound.
    /// Several of these subsystems decide what to build at construction
    /// (a storyboard that was never loaded can't be cheaply un-loaded
    /// mid-run), and a hot flip would leave a torn mix of cheap and
    /// expensive surfaces. The settings toggle therefore prompts for a
    /// restart on change (see
    /// <see cref="osu.Game.Overlays.Settings.PotatoModeToggleAndRestart"/>).
    ///
    /// Crucially this never writes back to the user's own graphics
    /// settings: it only short-circuits the read sites. Turning Potato
    /// off and restarting therefore returns every option to exactly what
    /// the user had before, with nothing to restore.
    /// </summary>
    public static class PotatoMode
    {
        private static bool active;

        /// <summary>
        /// True when Potato Mode was enabled at app startup. Immutable for
        /// the process lifetime; toggling the setting only takes effect
        /// after a restart.
        /// </summary>
        public static bool Active => active;

        /// <summary>
        /// Pin the Potato Mode flag from config before the visual tree is
        /// built. Called by <c>OsuGameBase.load()</c>.
        /// Idempotent; safe to call from tests.
        /// </summary>
        public static void SetFromConfig(bool enabled) => active = enabled;
    }
}
