// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Screens.Menu;

namespace osu.Game.Tests.Visual.Menus
{
    /// <summary>
    /// TumThai: the intro the game uses, which goes straight to the menu without osu!'s intro audio.
    /// </summary>
    [TestFixture]
    public partial class TestSceneIntroTumThai : IntroTestScene
    {
        protected override bool IntroReliesOnTrack => false;
        protected override IntroScreen CreateScreen() => new IntroTumThai();
    }
}
