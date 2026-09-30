// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Online;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class TestSomtumEndpointConfiguration
    {
        [Test]
        public void TestEndpointsMatchServerDeployment()
        {
            var config = new SomtumEndpointConfiguration();

            Assert.That(config.APIUrl, Is.EqualTo("https://osu.sundei.eu"));
            Assert.That(config.WebsiteUrl, Is.EqualTo("https://osu.sundei.eu"));

            Assert.That(config.APIClientID, Is.Empty);
            Assert.That(config.APIClientSecret, Is.Empty);

            Assert.That(config.SpectatorUrl, Is.EqualTo("https://spectator.sundei.eu/spectator"));
            Assert.That(config.MultiplayerUrl, Is.EqualTo("https://spectator.sundei.eu/multiplayer"));
            Assert.That(config.MetadataUrl, Is.EqualTo("https://spectator.sundei.eu/metadata"));
        }

        [Test]
        public void TestNoTrailingSlashesOnUrls()
        {
            var config = new SomtumEndpointConfiguration();

            Assert.That(config.APIUrl, Does.Not.EndWith("/"));
            Assert.That(config.WebsiteUrl, Does.Not.EndWith("/"));
        }
    }
}
