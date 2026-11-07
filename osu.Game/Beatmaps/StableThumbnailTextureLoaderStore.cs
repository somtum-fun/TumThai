// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// TumThai!Next: osu!stable's song select thumbnails. The whole background scaled into a 4:3 box (only the sides of a
    /// wide image are trimmed), unlike <see cref="BeatmapPanelBackgroundTextureLoaderStore"/>'s wide strip, which looks
    /// zoomed in when shown at 4:3. Kept small (stable's 160x120 thumbnail at 2x), so a listing doesn't hold full backgrounds.
    /// </summary>
    public class StableThumbnailTextureLoaderStore : IResourceStore<TextureUpload>
    {
        private const int width = 320;
        private const int height = 240;

        private readonly IResourceStore<TextureUpload>? textureStore;

        public StableThumbnailTextureLoaderStore(IResourceStore<TextureUpload>? textureStore)
        {
            this.textureStore = textureStore;
        }

        public void Dispose()
        {
            textureStore?.Dispose();
        }

        public TextureUpload Get(string name)
        {
            var textureUpload = textureStore?.Get(name);

            // NRT not enabled on framework side classes (IResourceStore / TextureLoaderStore).
            if (textureUpload == null)
                return null!;

            return makeThumbnail(textureUpload);
        }

        public async Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = new CancellationToken())
        {
            if (textureStore == null)
                return null!;

            var textureUpload = await textureStore.GetAsync(name, cancellationToken).ConfigureAwait(false);

            if (textureUpload == null)
                return null!;

            return await Task.Run(() => makeThumbnail(textureUpload), cancellationToken).ConfigureAwait(false);
        }

        private static TextureUpload makeThumbnail(TextureUpload textureUpload)
        {
            var image = Image.LoadPixelData(textureUpload.Data, textureUpload.Width, textureUpload.Height);

            // The original texture upload will no longer be returned or used.
            textureUpload.Dispose();

            // ResizeMode.Crop: scale to cover the 4:3 box, then trim what's left over (centred), as osu!stable's thumbnails.
            image.Mutate(i => i.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Crop,
            }));

            return new TextureUpload(image);
        }

        public Stream? GetStream(string name) => textureStore?.GetStream(name);

        public IEnumerable<string> GetAvailableResources() => textureStore?.GetAvailableResources() ?? Array.Empty<string>();
    }
}
