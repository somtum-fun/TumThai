// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class OsuConfigManagerLegacyServerTest
    {
        /// <summary>
        /// Installs that still hold a previously shipped default server should be moved back onto the
        /// current defaults on load, rather than staying pinned to a server no longer targeted.
        /// </summary>
        [Test]
        public void TestLegacyServerDefaultsAreMigratedOnLoad()
        {
            using (var storage = new TemporaryNativeStorage("osu-config-legacy-server-test"))
            {
                using (var config = new OsuConfigManager(storage))
                {
                    config.SetValue(OsuSetting.CustomApiUrl, "lazer.freedomdive.dev");
                    config.SetValue(OsuSetting.CustomAvatarUrl, "a.freedomdive.dev");
                    config.Save();
                }

                using (var config = new OsuConfigManager(storage))
                {
                    // both empty means "follow the built-in server".
                    Assert.That(config.Get<string>(OsuSetting.CustomApiUrl), Is.Empty);
                    Assert.That(config.Get<string>(OsuSetting.CustomAvatarUrl), Is.Empty);
                }
            }
        }

        /// <summary>
        /// A server the user chose themselves must survive loading untouched.
        /// </summary>
        [Test]
        public void TestUserConfiguredServerIsPreserved()
        {
            using (var storage = new TemporaryNativeStorage("osu-config-user-server-test"))
            {
                using (var config = new OsuConfigManager(storage))
                {
                    config.SetValue(OsuSetting.CustomApiUrl, "osu.example.com");
                    config.SetValue(OsuSetting.CustomAvatarUrl, "a.example.com");
                    config.Save();
                }

                using (var config = new OsuConfigManager(storage))
                {
                    Assert.That(config.Get<string>(OsuSetting.CustomApiUrl), Is.EqualTo("osu.example.com"));
                    Assert.That(config.Get<string>(OsuSetting.CustomAvatarUrl), Is.EqualTo("a.example.com"));
                }
            }
        }
    }
}
