// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Game.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.Utils
{
    [TestFixture]
    public class ImageFormatRestrictionsTest
    {
        [Test]
        public void TestTiffIsRefusedAndPngStillLoads()
        {
            ImageFormatRestrictions.Apply();

            using var image = new Image<Rgba32>(4, 4);

            using var tiff = new MemoryStream();
            image.SaveAsTiff(tiff);
            tiff.Position = 0;
            Assert.Throws<UnknownImageFormatException>(() => Image.Load<Rgba32>(tiff));

            using var png = new MemoryStream();
            image.SaveAsPng(png);
            png.Position = 0;
            using var loaded = Image.Load<Rgba32>(png);
            Assert.That(loaded.Width, Is.EqualTo(4));
        }
    }
}
