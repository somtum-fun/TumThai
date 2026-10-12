// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Screens;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// TumThai: the only startup "intro". It goes straight to the main menu's circle, with no "welcome to osu!" voice
    /// and none of osu!'s intro tracks: the menu plays a random beatmap of the player's, or nothing if they have none yet.
    /// </summary>
    public partial class IntroTumThai : IntroScreen
    {
        // Never used: UsesOsuAudio is false, so no themed intro beatmap is looked up or imported.
        protected override string BeatmapHash => string.Empty;
        protected override string BeatmapFile => string.Empty;

        protected override bool UsesOsuAudio => false;

        private const float logo_fade_in = 300;

        public IntroTumThai(Func<MainMenu>? createNextScreen = null)
            : base(createNextScreen)
        {
        }

        protected override void LogoArriving(OsuLogo logo, bool resuming)
        {
            base.LogoArriving(logo, resuming);

            if (!resuming)
            {
                FadeInBackground(logo_fade_in);

                logo.ScaleTo(1);
                logo.FadeIn(logo_fade_in);

                StartTrack();
                PrepareMenuLoad();

                Scheduler.AddDelayed(LoadMenu, logo_fade_in);
            }
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            this.FadeOut(300);
            base.OnSuspending(e);
        }
    }
}
