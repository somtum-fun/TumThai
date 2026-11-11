// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Performance;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class PanelBeatmapStandalone : Panel
    {
        public const float HEIGHT = CarouselItem.DEFAULT_HEIGHT * 1.6f;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private IBindable<StarDifficulty>? starDifficultyBindable;
        private CancellationTokenSource? starDifficultyCancellationSource;
        private string? lastIconRulesetShortName;

        private PanelSetBackground beatmapBackground = null!;
        private ScheduledDelegate? scheduledBackgroundRetrieval;

        private OsuSpriteText titleText = null!;
        private OsuSpriteText artistText = null!;
        private PanelUpdateBeatmapButton updateButton = null!;
        private BeatmapSetOnlineStatusPill statusPill = null!;

        private ConstrainedIconContainer difficultyIcon = null!;
        private StarRatingDisplay starRatingDisplay = null!;
        private SpreadDisplay spreadDisplay = null!;
        private StableStarRatingRow stableStars = null!;
        private Container stableThumbContainer = null!;
        private Sprite stableThumbSprite = null!;
        private PanelLocalRankDisplay localRank = null!;
        private OsuSpriteText keyCountText = null!;
        private OsuSpriteText difficultyText = null!;
        private OsuSpriteText authorText = null!;
        private FillFlowContainer mainFill = null!;
        private FillFlowContainer starFill = null!;

        private Box backgroundBorder = null!;
        private Drawable themedTintLayer = null!;

        // osu!stable standalone panel colours: orange when unselected, solid white when selected.
        private static readonly Color4 stable_orange = Color4Extensions.FromHex(@"E96800");
        private static readonly Color4 stable_orange_dark = Color4Extensions.FromHex(@"BA5300");
        private static readonly Color4 stable_selected_white = Color4.White;
        private static readonly Color4 stable_selected_white_edge = Color4Extensions.FromHex(@"E6E6E6");

        private BeatmapInfo beatmap => ((GroupedBeatmap)Item!.Model).Beatmap;

        public PanelBeatmapStandalone()
        {
            PanelXOffset = 20;
        }

        [BackgroundDependencyLoader]
        private void load(SkinManager skinManager)
        {
            Height = HEIGHT;
            var menuButtonBgTex = skinManager.GetClassicTexture(@"menu-button-background");

            Icon = difficultyIcon = new ConstrainedIconContainer
            {
                Size = new Vector2(12),
                Margin = new MarginPadding { Left = 4f, Right = 3f },
                Colour = colourProvider.Background5,
            };

            Background = backgroundBorder = new Box
            {
                RelativeSizeAxes = Axes.Both,
            };

            Content.Children = new Drawable[]
            {
                beatmapBackground = new PanelSetBackground(),
                themedTintLayer = menuButtonBgTex != null
                    ? new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(1.02f, 1.22f),
                        FillMode = FillMode.Stretch,
                        Texture = menuButtonBgTex,
                        Alpha = 0,
                    }
                    : new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                    },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Spacing = new Vector2(5),
                    Margin = new MarginPadding { Left = 6.5f },
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        stableThumbContainer = new Container
                        {
                            Size = new Vector2(72, 54),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Masking = true,
                            CornerRadius = 4,
                            Alpha = 0,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black.Opacity(0.5f),
                                },
                                stableThumbSprite = new Sprite
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    FillMode = FillMode.Fill,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                }
                            }
                        },
                        localRank = new PanelLocalRankDisplay
                        {
                            Scale = new Vector2(0.8f),
                            Origin = Anchor.CentreLeft,
                            Anchor = Anchor.CentreLeft,
                        },
                        mainFill = new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Bottom = 4.8f },
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                titleText = new OsuSpriteText
                                {
                                    Font = OsuFont.Style.Heading2.With(typeface: Typeface.TorusAlternate, weight: FontWeight.Bold),
                                },
                                artistText = new OsuSpriteText
                                {
                                    Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                    Padding = new MarginPadding { Top = -2 },
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    AutoSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Top = 2, Bottom = 2 },
                                    Children = new Drawable[]
                                    {
                                        statusPill = new BeatmapSetOnlineStatusPill
                                        {
                                            Animated = false,
                                            Origin = Anchor.BottomLeft,
                                            Anchor = Anchor.BottomLeft,
                                            TextSize = OsuFont.Style.Caption2.Size,
                                            Margin = new MarginPadding { Right = 4f },
                                        },
                                        updateButton = new PanelUpdateBeatmapButton
                                        {
                                            Scale = new Vector2(0.8f),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Margin = new MarginPadding { Right = 4f, Bottom = -1f },
                                        },
                                        keyCountText = new OsuSpriteText
                                        {
                                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Alpha = 0,
                                        },
                                        difficultyText = new OsuSpriteText
                                        {
                                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Margin = new MarginPadding { Right = 3f },
                                        },
                                        authorText = new OsuSpriteText
                                        {
                                            Colour = colourProvider.Content2,
                                            Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft
                                        }
                                    }
                                },
                                starFill = new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(3),
                                    AutoSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        starRatingDisplay = new StarRatingDisplay(default, StarRatingDisplaySize.Small, animated: !PotatoMode.UltraActive)
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Scale = new Vector2(0.875f),
                                        },
                                        spreadDisplay = new SpreadDisplay
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Selected = { BindTarget = Selected },
                                        }
                                    },
                                },
                                stableStars = new StableStarRatingRow
                                {
                                    Alpha = 0,
                                }
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => updateKeyCount());
            mods.BindValueChanged(_ => updateKeyCount(), true);

            Selected.BindValueChanged(s =>
            {
                Expanded.Value = s.NewValue;
            }, true);
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            var beatmapSet = beatmap.BeatmapSet!;

            if (!PotatoMode.UltraActive)
            {
                scheduledBackgroundRetrieval = Scheduler.AddDelayed(b =>
                {
                    var working = beatmaps.GetWorkingBeatmap(b);
                    beatmapBackground.Beatmap = working;
                    stableThumbSprite.Texture = working.GetStableThumbnail();
                }, beatmap, 50);
            }

            titleText.Text = new RomanisableString(beatmapSet.Metadata.TitleUnicode, beatmapSet.Metadata.Title);
            artistText.Text = new RomanisableString(beatmapSet.Metadata.ArtistUnicode, beatmapSet.Metadata.Artist);
            updateButton.BeatmapSet = beatmapSet;
            statusPill.Status = beatmap.Status;

            if (!PotatoMode.UltraActive)
            {
                if (lastIconRulesetShortName != beatmap.Ruleset.ShortName)
                {
                    lastIconRulesetShortName = beatmap.Ruleset.ShortName;
                    difficultyIcon.Icon = beatmap.Ruleset.CreateInstance().CreateIcon();
                }

                difficultyIcon.Show();
            }

            localRank.Beatmap = beatmap;
            difficultyText.Text = beatmap.DifficultyName;
            authorText.Text = BeatmapsetsStrings.ShowDetailsMappedBy(beatmap.Metadata.Author.Username);

            computeStarRating();
            spreadDisplay.Beatmap.Value = beatmap;
            updateKeyCount();
            ApplyInterfaceTheme();
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            scheduledBackgroundRetrieval?.Cancel();
            scheduledBackgroundRetrieval = null;
            beatmapBackground.Beatmap = null;
            stableThumbSprite.Texture = null;
            updateButton.BeatmapSet = null;
            localRank.Beatmap = null;
            starDifficultyBindable = null;
            spreadDisplay.Beatmap.Value = null;

            starDifficultyCancellationSource?.Cancel();
        }

        protected override void ApplyInterfaceTheme()
        {
            base.ApplyInterfaceTheme();

            if (CurrentInterfaceTheme.IsThemed())
            {
                bool isSelected = Selected.Value;

                if (themedTintLayer is Sprite s && SkinManager != null)
                    s.Texture = SkinManager.GetClassicTexture(@"menu-button-background");

                themedTintLayer.Alpha = isSelected ? 0.96f : 0.94f;
                if (isSelected)
                    themedTintLayer.Colour = stable_selected_white;
                else if (CurrentInterfaceTheme == InterfaceTheme.OsuStable && Item != null)
                    themedTintLayer.Colour = beatmap.LastPlayed != null ? stable_orange : Color4Extensions.FromHex(@"EB4999");
                else
                    themedTintLayer.Colour = CurrentInterfaceTheme.SetCardTint();

                Color4 activeText = SkinManager?.GetSongSelectActiveTextColour() ?? Color4.Black;
                Color4 inactiveText = SkinManager?.GetSongSelectInactiveTextColour() ?? Color4.White;

                var fgColour = isSelected ? activeText : inactiveText;
                var secondaryColour = isSelected ? activeText.Opacity(0.85f) : inactiveText.Opacity(0.85f);

                titleText.Colour = fgColour;
                artistText.Colour = secondaryColour;
                difficultyText.Colour = fgColour;
                keyCountText.Colour = fgColour;
                authorText.Colour = secondaryColour;
                authorText.Alpha = 0;

                if (Item != null && !string.IsNullOrEmpty(beatmap.Metadata.Author.Username))
                    artistText.Text = $"{beatmap.Metadata.Artist} // {beatmap.Metadata.Author.Username}";

                statusPill.Alpha = 0;
                starFill.Alpha = 0;
                stableStars.Alpha = 1;
                stableStars.ActiveWhiteCard = isSelected;
                stableThumbContainer.Alpha = 1;
                beatmapBackground.Alpha = 0;
            }
            else
            {
                themedTintLayer.Alpha = 0;
                titleText.Colour = Color4.White;
                artistText.Colour = Color4.White;
                difficultyText.Colour = Color4.White;
                keyCountText.Colour = Color4.White;
                authorText.Colour = colourProvider.Content2;
                authorText.Alpha = 1;

                statusPill.Alpha = 1;
                starFill.Alpha = 1;
                stableStars.Alpha = 0;
                stableThumbContainer.Alpha = 0;
                beatmapBackground.Alpha = 1;
            }
        }

        private void computeStarRating()
        {
            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource = new CancellationTokenSource();

            if (Item == null)
                return;

            var initialStored = new StarDifficulty(beatmap.StarRating, 0);
            stableStars.Stars = initialStored.Stars;

            // Potato mode: use the stored nomod star rating instead of recalculating per panel while scrolling.
            if (PotatoMode.Active)
            {
                starRatingDisplay.Current.Value = initialStored;
                spreadDisplay.StarDifficulty.Value = initialStored;
                return;
            }

            starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmap, starDifficultyCancellationSource.Token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            starDifficultyBindable.BindValueChanged(starDifficulty =>
            {
                starRatingDisplay.Current.Value = starDifficulty.NewValue;
                spreadDisplay.StarDifficulty.Value = starDifficulty.NewValue;
                if (starDifficulty.NewValue.Stars > 0 || beatmap.StarRating <= 0)
                    stableStars.Stars = starDifficulty.NewValue.Stars;
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            if (Item?.IsVisible != true)
            {
                starDifficultyCancellationSource?.Cancel();
                starDifficultyCancellationSource = null;
            }

            // Dirty hack to make sure we don't take up spacing in parent fill flow when not displaying a rank.
            // I can't find a better way to do this.
            mainFill.Margin = new MarginPadding { Left = 1 / starRatingDisplay.Scale.X * (localRank.HasRank ? 0 : -3) };

            if (CurrentInterfaceTheme.IsThemed())
            {
                FitStableTextBlock(mainFill);

                var borderColour = Selected.Value ? Color4.White : CurrentInterfaceTheme.SetCardTint();

                if (AccentColour != borderColour)
                {
                    AccentColour = borderColour;
                    backgroundBorder.Colour = borderColour;
                }

                var iconColour = Selected.Value ? Color4.Black : Color4.White;
                if (difficultyIcon.Colour != iconColour)
                    difficultyIcon.Colour = iconColour;
            }
            else
            {
                var diffColour = starRatingDisplay.DisplayedDifficultyColour;

                if (AccentColour != diffColour)
                {
                    AccentColour = diffColour;
                    spreadDisplay.Current.Colour = diffColour;
                    backgroundBorder.Colour = diffColour;
                }

                if (difficultyIcon.Colour != starRatingDisplay.DisplayedDifficultyTextColour)
                    difficultyIcon.Colour = starRatingDisplay.DisplayedDifficultyTextColour;
            }
        }

        private void updateKeyCount()
        {
            if (Item == null)
                return;

            if (ruleset.Value.ShortName is @"osu" or @"taiko" or @"fruits")
            {
                keyCountText.Alpha = 0;
                return;
            }

            var rulesetInstance = ruleset.Value.CreateInstance();

            if (rulesetInstance.GameplayVariants.Count() > 1)
            {
                int variant = rulesetInstance.GetVariantForBeatmap(beatmap, mods.Value);
                var variantName = rulesetInstance.GetVariantName(variant);

                keyCountText.Alpha = 1;
                keyCountText.Text = LocalisableString.Interpolate($"[{variantName}] ");
            }
            else
                keyCountText.Alpha = 0;
        }

        public override MenuItem[] ContextMenuItems
        {
            get
            {
                if (Item == null)
                    return Array.Empty<MenuItem>();

                List<MenuItem> items = new List<MenuItem>();

                if (songSelect != null)
                    items.AddRange(songSelect.GetForwardActions(beatmap));

                return items.ToArray();
            }
        }
    }
}
