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

            Assert.That(config.APIUrl, Is.EqualTo("https://osu.blueskychan.dev"));
            Assert.That(config.WebsiteUrl, Is.EqualTo("https://osu.blueskychan.dev"));

            // the server's token endpoint rejects a sign-in outright when these are not sent.
            Assert.That(config.APIClientID, Is.EqualTo("5"));
            Assert.That(config.APIClientSecret, Is.Not.Empty);

            Assert.That(config.SpectatorUrl, Is.EqualTo("https://spectator.blueskychan.dev/spectator"));
            Assert.That(config.MultiplayerUrl, Is.EqualTo("https://spectator.blueskychan.dev/multiplayer"));
            Assert.That(config.MetadataUrl, Is.EqualTo("https://spectator.blueskychan.dev/metadata"));

            // the editor only offers submission when this is set.
            Assert.That(config.BeatmapSubmissionServiceUrl, Is.EqualTo("https://osu.blueskychan.dev/beatmap-submission"));
        }

        [Test]
        public void TestNoTrailingSlashesOnUrls()
        {
            var config = new SomtumEndpointConfiguration();

            Assert.That(config.APIUrl, Does.Not.EndWith("/"));
            Assert.That(config.WebsiteUrl, Does.Not.EndWith("/"));
        }

        /// <summary>
        /// The avatar host must stay consistent with the API host, so that renaming the server is a single
        /// edit and the two cannot drift apart.
        /// </summary>
        [Test]
        public void TestAvatarHostMatchesApiHostByDefault()
        {
            Assert.That(SomtumEndpointConfiguration.ResolveAvatarHost(null, null), Is.EqualTo(SomtumEndpointConfiguration.AVATAR_HOST));
            Assert.That(SomtumEndpointConfiguration.AVATAR_HOST, Is.EqualTo("a." + ServerHost.RegistrableDomain(SomtumEndpointConfiguration.API_HOST)));
        }

        [Test]
        public void TestAvatarHostFollowsCustomServer()
        {
            Assert.That(SomtumEndpointConfiguration.ResolveAvatarHost("osu.example.com", null), Is.EqualTo("a.example.com"));
            Assert.That(SomtumEndpointConfiguration.ResolveAvatarHost("https://osu.example.com/", null), Is.EqualTo("a.example.com"));
        }

        [Test]
        public void TestExplicitAvatarHostWinsOverTheServer()
        {
            Assert.That(SomtumEndpointConfiguration.ResolveAvatarHost("osu.example.com", "cdn.elsewhere.net"), Is.EqualTo("cdn.elsewhere.net"));
            Assert.That(SomtumEndpointConfiguration.ResolveAvatarHost(null, "https://cdn.elsewhere.net/"), Is.EqualTo("cdn.elsewhere.net"));
        }

        /// <summary>
        /// A custom server goes through the same class, so it cannot disagree with the built-in one about
        /// which properties get set. An arbitrary server is assumed to host its hubs under /signalr.
        /// </summary>
        [Test]
        public void TestCustomServerPopulatesEverySameEndpoint()
        {
            var config = new SomtumEndpointConfiguration("osu.example.com");

            Assert.That(config.APIUrl, Is.EqualTo("https://osu.example.com"));
            Assert.That(config.WebsiteUrl, Is.EqualTo("https://osu.example.com"));
            Assert.That(config.SpectatorUrl, Is.EqualTo("https://osu.example.com/signalr/spectator"));
            Assert.That(config.MultiplayerUrl, Is.EqualTo("https://osu.example.com/signalr/multiplayer"));
            Assert.That(config.MetadataUrl, Is.EqualTo("https://osu.example.com/signalr/metadata"));
            Assert.That(config.BeatmapSubmissionServiceUrl, Is.EqualTo("https://osu.example.com/beatmap-submission"));

            // client credentials must be sent whichever server is in use.
            Assert.That(config.APIClientID, Is.EqualTo("5"));
            Assert.That(config.APIClientSecret, Is.Not.Empty);
        }

        [Test]
        public void TestCustomServerAcceptsSchemeAndTrailingSlash()
        {
            var config = new SomtumEndpointConfiguration("https://osu.example.com/");

            Assert.That(config.APIUrl, Is.EqualTo("https://osu.example.com"));
            Assert.That(config.SpectatorUrl, Is.EqualTo("https://osu.example.com/signalr/spectator"));
        }

        [Test]
        public void TestCustomServerWebsiteHostIsDerivedFromApiPrefix()
        {
            Assert.That(new SomtumEndpointConfiguration("api.example.com").WebsiteUrl, Is.EqualTo("https://example.com"));
            Assert.That(new SomtumEndpointConfiguration("osu-api.example.com").WebsiteUrl, Is.EqualTo("https://osu.example.com"));
        }

        [Test]
        public void TestEmptyCustomServerFallsBackToBuiltIn()
        {
            foreach (string? value in new[] { null, "", "   " })
                Assert.That(new SomtumEndpointConfiguration(value).APIUrl, Is.EqualTo($"https://{SomtumEndpointConfiguration.API_HOST}"));
        }
    }
}
