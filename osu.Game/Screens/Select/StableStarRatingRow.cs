// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// TumThai!Next: osu!stable 10-star difficulty row on beatmap carousel cards.
    /// </summary>
    public partial class StableStarRatingRow : CompositeDrawable
    {
        private const int star_count = 10;
        private const float star_scale = 0.6f;

        private readonly Sprite[] bgStars = new Sprite[star_count];
        private readonly Sprite[] fgStars = new Sprite[star_count];

        private double stars;
        private bool activeWhiteCard;

        public double Stars
        {
            get => stars;
            set
            {
                if (stars == value)
                    return;

                stars = value;
                if (IsLoaded)
                    updateStars();
            }
        }

        public bool ActiveWhiteCard
        {
            get => activeWhiteCard;
            set
            {
                if (activeWhiteCard == value)
                    return;

                activeWhiteCard = value;
                if (IsLoaded)
                    updateStars();
            }
        }

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private osu.Framework.Bindables.IBindable<Skin>? currentSkin;

        public StableStarRatingRow()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var starTex = skinManager.GetClassicTexture(@"star");

            var row = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
            };

            // osu!stable (BeatmapTreeItem): each star is the skin's star at its own size times 0.6, centred in a slot as wide as
            // that, so a skin's large or odd-shaped star.png shows as the skin made it (never squeezed into a fixed box).
            for (int i = 0; i < star_count; i++)
            {
                row.Add(slots[i] = new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Children = new Drawable[]
                    {
                        bgStars[i] = new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                        fgStars[i] = new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                    },
                });
            }

            InternalChild = row;
            applyTexture(starTex);
            updateStars();
        }

        private readonly Container[] slots = new Container[star_count];

        private void applyTexture(Texture? starTex)
        {
            Vector2 slot = starTex != null ? new Vector2(starTex.DisplayWidth, starTex.DisplayHeight) * star_scale : new Vector2(15f);

            for (int i = 0; i < star_count; i++)
            {
                slots[i].Size = slot;
                bgStars[i].Texture = starTex;
                fgStars[i].Texture = starTex;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentSkin = skinManager.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ =>
            {
                applyTexture(skinManager.GetClassicTexture(@"star"));
                updateStars();
            });
        }

        private void updateStars()
        {
            Color4 fgColour = activeWhiteCard
                ? skinManager.GetSongSelectActiveTextColour()
                : skinManager.GetSongSelectInactiveTextColour();
            Color4 bgColour = activeWhiteCard
                ? fgColour.Opacity(0.22f)
                : new Color4(255, 255, 255, 30);

            for (int i = 0; i < star_count; i++)
            {
                float fill = (float)Math.Clamp(stars - i, 0.0, 1.0);

                bgStars[i].Colour = bgColour;
                bgStars[i].Scale = new Vector2(star_scale);

                fgStars[i].Colour = fgColour;
                fgStars[i].Alpha = fill > 0 ? 1f : 0f;
                fgStars[i].Scale = fill > 0
                    ? new Vector2(star_scale * (float)Math.Max(0.5, fill + i * 0.04))
                    : Vector2.Zero;
            }
        }
    }
}
