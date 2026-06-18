// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Users.Drawables
{
    [LongRunningLoad]
    public partial class DrawableAvatar : Sprite
    {
        private readonly IUser user;

        /// <summary>
        /// A simple, non-interactable avatar sprite for the specified user.
        /// </summary>
        /// <param name="user">The user. A null value will get a placeholder avatar.</param>
        public DrawableAvatar(IUser user = null)
        {
            this.user = user;

            RelativeSizeAxes = Axes.Both;
            FillMode = FillMode.Fit;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures, OsuConfigManager config)
        {
            if (user != null && user.OnlineID > 1)
                // TODO: The fallback here should not need to exist. Users should be looked up and populated via UserLookupCache or otherwise
                // in remaining cases where this is required (chat tabs, local leaderboard), at which point this should be removed.
                Texture = textures.Get((user as APIUser)?.AvatarUrl ?? $@"https://{getAvatarHost(config)}/{user.OnlineID}");

            Texture ??= textures.Get(@"Online/avatar-guest");
        }

        /// <summary>
        /// The host used to look up avatars when the API doesn't provide an explicit URL.
        /// Configurable via <see cref="OsuSetting.CustomAvatarUrl"/>; defaults to <c>a.freedomdive.dev</c>.
        /// </summary>
        private const string default_avatar_host = @"a.freedomdive.dev";

        private static string getAvatarHost(OsuConfigManager config)
        {
            string host = config.Get<string>(OsuSetting.CustomAvatarUrl) ?? string.Empty;

            // accept a pasted scheme/path but reduce to a bare host so we can format https://{host}/{id}.
            host = Regex.Replace(host.Trim(), @"^\s*https?://", string.Empty, RegexOptions.IgnoreCase);

            int slash = host.IndexOf('/');
            if (slash >= 0)
                host = host.Substring(0, slash);

            host = host.Trim().Trim('/');

            return string.IsNullOrEmpty(host) ? default_avatar_host : host;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(300, Easing.OutQuint);
        }
    }
}
