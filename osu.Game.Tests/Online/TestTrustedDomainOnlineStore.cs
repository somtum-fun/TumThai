// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Online;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class TestTrustedDomainOnlineStore
    {
        /// <summary>
        /// Builds the store the way the game does for the built-in server.
        /// </summary>
        private static TrustedDomainOnlineStore storeForDefaultServer()
        {
            var endpoints = new SomtumEndpointConfiguration();

            return new TrustedDomainOnlineStore(endpoints.APIUrl, SomtumEndpointConfiguration.ResolveAvatarHost(null, null));
        }

        /// <summary>
        /// The hosts that actually serve this server's avatars and covers. A store that rejects these makes
        /// every avatar fall back to the bundled placeholder, which is what happened when the asset store
        /// built its own filter trusting only the official hosts.
        /// </summary>
        [Test]
        public void TestConfiguredServerAssetsAreTrusted()
        {
            var store = storeForDefaultServer();

            Assert.That(store.IsTrusted(SomtumEndpointConfiguration.API_HOST), Is.True);
            Assert.That(store.IsTrusted(SomtumEndpointConfiguration.AVATAR_HOST), Is.True);
            Assert.That(store.IsTrusted(SomtumEndpointConfiguration.SPECTATOR_HOST), Is.True);
        }

        [Test]
        public void TestOfficialHostsRemainTrusted()
        {
            Assert.That(storeForDefaultServer().IsTrusted(@"a.ppy.sh"), Is.True);
            Assert.That(storeForDefaultServer().IsTrusted(@"assets.ppy.sh"), Is.True);
        }

        [Test]
        public void TestUnrelatedHostsAreNotTrusted()
        {
            var store = storeForDefaultServer();

            Assert.That(store.IsTrusted(@"evil.example.com"), Is.False);
            Assert.That(store.IsTrusted(@"blueskychan.dev.evil.example.com"), Is.False);
        }

        /// <summary>
        /// A custom server's own subdomains must be trusted, since that is where it serves its assets.
        /// </summary>
        [Test]
        public void TestCustomServerSubdomainsAreTrusted()
        {
            var store = new TrustedDomainOnlineStore(@"https://osu.example.com", @"a.example.com");

            Assert.That(store.IsTrusted(@"osu.example.com"), Is.True);
            Assert.That(store.IsTrusted(@"a.example.com"), Is.True);
            Assert.That(store.IsTrusted(@"example.com"), Is.True);
            Assert.That(store.IsTrusted(@"somewhere.else.net"), Is.False);
        }

        /// <summary>
        /// A separately hosted avatar CDN is trusted through the avatar setting alone.
        /// </summary>
        [Test]
        public void TestAvatarHostOnAnotherDomainIsTrusted()
        {
            var store = new TrustedDomainOnlineStore(@"https://osu.example.com", @"cdn.elsewhere.net");

            Assert.That(store.IsTrusted(@"cdn.elsewhere.net"), Is.True);
            Assert.That(store.IsTrusted(@"other.elsewhere.net"), Is.True);
        }

        /// <summary>
        /// With nothing configured only the official hosts are trusted. This is what broke the asset path,
        /// so it is pinned deliberately. The constructor takes no defaults, so reaching this state now
        /// requires passing nulls on purpose.
        /// </summary>
        [Test]
        public void TestNothingConfiguredTrustsOnlyOfficialHosts()
        {
            var store = new TrustedDomainOnlineStore(null, null);

            Assert.That(store.IsTrusted(@"a.ppy.sh"), Is.True);
            Assert.That(store.IsTrusted(SomtumEndpointConfiguration.API_HOST), Is.False);
            Assert.That(store.IsTrusted(SomtumEndpointConfiguration.AVATAR_HOST), Is.False);
        }
    }
}
