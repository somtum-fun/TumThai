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

            Assert.That(config.APIUrl, Is.EqualTo("https://osu.somtum.fun"));
            Assert.That(config.WebsiteUrl, Is.EqualTo("https://osu.somtum.fun"));

            // the server's token endpoint rejects a sign-in outright when these are not sent.
            Assert.That(config.APIClientID, Is.EqualTo("5"));
            Assert.That(config.APIClientSecret, Is.Not.Empty);

            Assert.That(config.SpectatorUrl, Is.EqualTo("https://spectator.somtum.fun/spectator"));
            Assert.That(config.MultiplayerUrl, Is.EqualTo("https://spectator.somtum.fun/multiplayer"));
            Assert.That(config.MetadataUrl, Is.EqualTo("https://spectator.somtum.fun/metadata"));

            // the editor only offers submission when this is set.
            Assert.That(config.BeatmapSubmissionServiceUrl, Is.EqualTo("https://osu.somtum.fun/beatmap-submission"));
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

        [Test]
        public void TestPpyShIsBlockedExceptDevPpySh()
        {
            Assert.That(ServerHost.IsBlockedPpyHost("osu.ppy.sh"), Is.True);
            Assert.That(ServerHost.IsBlockedPpyHost("https://osu.ppy.sh/"), Is.True);
            Assert.That(ServerHost.IsBlockedPpyHost("ppy.sh"), Is.True);
            Assert.That(ServerHost.IsBlockedPpyHost("a.ppy.sh"), Is.True);

            Assert.That(ServerHost.IsBlockedPpyHost("dev.ppy.sh"), Is.False);
            Assert.That(ServerHost.IsBlockedPpyHost("https://dev.ppy.sh/"), Is.False);
            Assert.That(ServerHost.IsBlockedPpyHost("a.dev.ppy.sh"), Is.False);
            Assert.That(ServerHost.IsBlockedPpyHost("osu.somtum.fun"), Is.False);

            // Blocked ppy.sh falls back to built-in API_HOST
            Assert.That(new SomtumEndpointConfiguration("osu.ppy.sh").APIUrl, Is.EqualTo($"https://{SomtumEndpointConfiguration.API_HOST}"));
            Assert.That(SomtumEndpointConfiguration.ResolveAvatarHost("osu.ppy.sh", "a.ppy.sh"), Is.EqualTo(SomtumEndpointConfiguration.AVATAR_HOST));

            // dev.ppy.sh is allowed
            Assert.That(new SomtumEndpointConfiguration("dev.ppy.sh").APIUrl, Is.EqualTo("https://dev.ppy.sh"));
            Assert.That(SomtumEndpointConfiguration.ResolveAvatarHost("dev.ppy.sh", null), Is.EqualTo("a.dev.ppy.sh"));
        }

        [Test]
        public void TestExternalServerProfileValidationAndCustomClientCredentials()
        {
            var blockedProfile = new ExternalServerProfile { Name = "Official", ApiUrl = "osu.ppy.sh" };
            Assert.That(blockedProfile.Validate(out string err1), Is.False);
            Assert.That(err1, Does.Contain("dev.ppy.sh"));

            var devProfile = new ExternalServerProfile { Name = "Dev", ApiUrl = "dev.ppy.sh", ClientId = "42", ClientSecret = "secret123" };
            Assert.That(devProfile.Validate(out _), Is.True);

            var endpoints = new SomtumEndpointConfiguration(devProfile.ApiUrl, devProfile.WebsiteUrl, devProfile.ClientId, devProfile.ClientSecret);
            Assert.That(endpoints.APIUrl, Is.EqualTo("https://dev.ppy.sh"));
            Assert.That(endpoints.APIClientID, Is.EqualTo("42"));
            Assert.That(endpoints.APIClientSecret, Is.EqualTo("secret123"));
        }
    }
}
