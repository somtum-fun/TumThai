// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using osu.Game.Online;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    [NonParallelizable]
    public class TestTumThaiClient
    {
        [Test]
        public void TestHashesAreInStableFormatAndOffForOtherServers()
        {
            string idFile = Path.GetTempFileName();
            File.Delete(idFile);

            try
            {
                TumThaiClient.InstallIdPath = idFile;
                TumThaiClient.IsTomYumServer = true;

                string? hashes = TumThaiClient.Hashes;
                Assert.That(hashes, Is.Not.Null);

                // path_md5:adapters:adapters_md5:uninstall_md5:disk_md5: (the server's check, TomYumLazerClient::hashesFrom)
                Assert.That(hashes, Does.EndWith(":"));
                string[] parts = hashes![..^1].Split(':');
                Assert.That(parts, Has.Length.EqualTo(5));
                foreach (int i in new[] { 0, 2, 3 })
                    Assert.That(Regex.IsMatch(parts[i], "^[0-9a-f]{32}$"), Is.True, $"part {i}: {parts[i]}");
                Assert.That(parts[4], Is.Empty.Or.Match("^[0-9a-f]{32}$"));

                // the install id is kept, so the uninstall hash stays the same next time.
                Assert.That(File.Exists(idFile), Is.True);

                TumThaiClient.IsTomYumServer = false;
                Assert.That(TumThaiClient.Hashes, Is.Null);
            }
            finally
            {
                TumThaiClient.IsTomYumServer = true;
                File.Delete(idFile);
            }
        }
    }
}
