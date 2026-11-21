// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Framework.Testing;
using osu.Game.Input;
using osu.Game.Rulesets.Osu.Mods;
using osuTK;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>TumThai: osu!droid is playable on a touch screen PC once a finger has touched it (not a stylus).</summary>
    public partial class TestSceneTouchScreenDetection : OsuManualInputManagerTestScene
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new TouchInputInterceptor());
        }

        [SetUpSteps]
        public void SetUpSteps() => AddStep("no touch screen yet", () => TouchScreen.Detected = false);

        [Test]
        public void TestFingerMakesDroidPlayable()
        {
            AddAssert("osu!droid not playable", () => new OsuModDroid().UserPlayable, () => Is.EqualTo(osu.Framework.RuntimeInfo.IsMobile));

            AddStep("stylus touches", () =>
            {
                InputManager.BeginTouch(new Touch(TouchSource.PenTouch, ScreenSpaceDrawQuad.Centre));
                InputManager.EndTouch(new Touch(TouchSource.PenTouch, ScreenSpaceDrawQuad.Centre));
            });
            AddAssert("a stylus doesn't count", () => !TouchScreen.Detected);

            AddStep("finger touches", () =>
            {
                InputManager.BeginTouch(new Touch(TouchSource.Touch1, ScreenSpaceDrawQuad.Centre));
                InputManager.EndTouch(new Touch(TouchSource.Touch1, ScreenSpaceDrawQuad.Centre));
            });
            AddStep("mouse used again", () => InputManager.MoveMouseTo(ScreenSpaceDrawQuad.Centre + new Vector2(10)));

            AddAssert("touch screen detected", () => TouchScreen.Detected);
            AddAssert("osu!droid playable", () => new OsuModDroid().UserPlayable);
        }
    }
}
