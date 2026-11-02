// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Utils
{
    /// <summary>
    /// TumThai: TIFF files are refused as images. ImageSharp before 3.2.0 can write outside its buffer on a crafted tiled
    /// fax TIFF (GHSA-v76p-62qx-wwq2), and backgrounds and skins come from whoever made the map. 3.2.0 needs a paid-for or
    /// applied-for licence key, and nothing in the game uses TIFF, so the decoder is taken out of reach instead.
    /// </summary>
    public static class ImageFormatRestrictions
    {
        private static readonly IImageDecoder refused = new RefusedDecoder();

        public static void Apply() => SixLabors.ImageSharp.Configuration.Default.ImageFormatsManager.SetDecoder(TiffFormat.Instance, refused);

        private sealed class RefusedDecoder : IImageDecoder
        {
            private static UnknownImageFormatException refusal() => new UnknownImageFormatException("TIFF images aren't loaded.");

            public ImageInfo Identify(DecoderOptions options, Stream stream) => throw refusal();

            public Task<ImageInfo> IdentifyAsync(DecoderOptions options, Stream stream, CancellationToken cancellationToken = default) => throw refusal();

            public Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream)
                where TPixel : unmanaged, IPixel<TPixel> => throw refusal();

            public Image Decode(DecoderOptions options, Stream stream) => throw refusal();

            public Task<Image<TPixel>> DecodeAsync<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken = default)
                where TPixel : unmanaged, IPixel<TPixel> => throw refusal();

            public Task<Image> DecodeAsync(DecoderOptions options, Stream stream, CancellationToken cancellationToken = default) => throw refusal();
        }
    }
}
