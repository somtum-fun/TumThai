// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class TestAPIUserPlayStyles
    {
        [Test]
        public void TestTomYumsPlayStylesAreRead()
        {
            var user = JsonConvert.DeserializeObject<APIUser>(@"{""id"": 2, ""username"": ""a"", ""playstyle"": [""mouse"", ""eggplant"", ""touchpad""]}");

            Assert.That(user!.PlayStyles, Is.EqualTo(new[] { APIPlayStyle.Mouse, APIPlayStyle.Eggplant, APIPlayStyle.Touchpad }));
        }

        [Test]
        public void TestAnUnknownPlayStyleIsLeftOutInsteadOfFailingTheUser()
        {
            var user = JsonConvert.DeserializeObject<APIUser>(@"{""id"": 2, ""username"": ""a"", ""playstyle"": [""keyboard"", ""something new""]}");

            Assert.That(user!.Username, Is.EqualTo("a"));
            Assert.That(user.PlayStyles, Is.EqualTo(new[] { APIPlayStyle.Keyboard }));
        }
    }
}
