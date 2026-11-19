// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Performance;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class PotatoModeTest
    {
        [TearDown]
        public void TearDown()
        {
            PotatoMode.SetFromConfig(false);
        }

        [Test]
        public void TestConfigDefaultsAreDisabled()
        {
            using (var storage = new TemporaryNativeStorage("osu-config-potato-mode-test"))
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.PotatoMode), Is.False);
                Assert.That(config.Get<bool>(OsuSetting.UltraUltimatePotatoMode), Is.False);
            }
        }

        [Test]
        public void TestStandardPotatoModeState()
        {
            PotatoMode.SetFromConfig(true, false);

            Assert.That(PotatoMode.Active, Is.True);
            Assert.That(PotatoMode.UltraActive, Is.False);
        }

        [Test]
        public void TestUltraUltimatePotatoModeImpliesStandardPotatoMode()
        {
            PotatoMode.SetFromConfig(false, true);

            Assert.That(PotatoMode.Active, Is.True);
            Assert.That(PotatoMode.UltraActive, Is.True);
        }
    }
}
