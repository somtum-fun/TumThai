// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
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
                Spacing = new Vector2(1.5f, 0f),
            };

            for (int i = 0; i < star_count; i++)
            {
                row.Add(new Container
                {
                    Size = new Vector2(15f, 15f),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Children = new Drawable[]
                    {
                        bgStars[i] = new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(14f),
                            FillMode = FillMode.Fit,
                            Texture = starTex,
                            Blending = BlendingParameters.Additive,
                        },
                        fgStars[i] = new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(14f),
                            FillMode = FillMode.Fit,
                            Texture = starTex,
                        },
                    },
                });
            }

            InternalChild = row;
            updateStars();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentSkin = skinManager.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ =>
            {
                var starTex = skinManager.GetClassicTexture(@"star");
                for (int i = 0; i < star_count; i++)
                {
                    bgStars[i].Texture = starTex;
                    fgStars[i].Texture = starTex;
                }

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
                : new Color4(255, 255, 255, 35);

            for (int i = 0; i < star_count; i++)
            {
                float fill = (float)Math.Clamp(stars - i, 0.0, 1.0);

                bgStars[i].Colour = bgColour;
                bgStars[i].Scale = new Vector2(0.58f);

                fgStars[i].Colour = fgColour;
                fgStars[i].Alpha = fill > 0 ? 1f : 0f;
                fgStars[i].Scale = fill > 0
                    ? new Vector2(0.6f * (float)Math.Max(0.5, fill + i * 0.04))
                    : Vector2.Zero;
            }
        }
    }
}
