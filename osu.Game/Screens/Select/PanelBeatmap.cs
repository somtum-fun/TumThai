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
using osu.Framework.Graphics.Colour;
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
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
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
    public partial class PanelBeatmap : Panel
    {
        public const float HEIGHT = CarouselItem.DEFAULT_HEIGHT;

        private StarCounter starCounter = null!;
        private StableStarRatingRow stableStars = null!;
        private Container stableThumbContainer = null!;
        private PanelSetBackground stableThumbBackground = null!;
        private Sprite stableModeIcon = null!;
        private OsuSpriteText stableTitleText = null!;
        private OsuSpriteText stableArtistText = null!;
        private ScheduledDelegate? scheduledBackgroundRetrieval;
        private ConstrainedIconContainer difficultyIcon = null!;
        private OsuSpriteText variantText = null!;
        private StarRatingDisplay starRatingDisplay = null!;
        private PanelLocalRankDisplay localRank = null!;
        private OsuSpriteText difficultyText = null!;
        private OsuSpriteText authorText = null!;
        private FillFlowContainer leftRowFill = null!;
        private FillFlowContainer mainFill = null!;

        private IBindable<StarDifficulty>? starDifficultyBindable;
        private CancellationTokenSource? starDifficultyCancellationSource;

        private Box backgroundBorder = null!;
        private Box backgroundDifficultyTint = null!;
        private Box lazerContentBackground = null!;
        private Drawable contentBackground = null!;

        private TrianglesV2 triangles = null!;

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private BeatmapInfo beatmap => ((GroupedBeatmap)Item!.Model).Beatmap;

        public PanelBeatmap()
        {
            PanelXOffset = 60;
        }

        [BackgroundDependencyLoader]
        private void load(SkinManager skinManager)
        {
            Height = HEIGHT;
            var menuButtonBgTex = skinManager.GetClassicTexture(@"menu-button-background");

            Icon = difficultyIcon = new ConstrainedIconContainer
            {
                Size = new Vector2(9f),
                Margin = new MarginPadding { Left = 2.5f, Right = 1.5f },
                Colour = colourProvider.Background5,
            };

            Background = backgroundBorder = new Box
            {
                RelativeSizeAxes = Axes.Both,
            };

            Content.Children = new Drawable[]
            {
                lazerContentBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4),
                    Alpha = CurrentInterfaceTheme.IsThemed() ? 0 : 1,
                },
                contentBackground = menuButtonBgTex != null
                    ? new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(1.02f, 1.22f),
                        FillMode = FillMode.Stretch,
                        Texture = menuButtonBgTex,
                        Colour = CurrentInterfaceTheme.DifficultyRowBackground(ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4)),
                        Alpha = CurrentInterfaceTheme.IsThemed() ? 1 : 0,
                    }
                    : new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = CurrentInterfaceTheme.DifficultyRowBackground(ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4)),
                        Alpha = CurrentInterfaceTheme.IsThemed() ? 1 : 0,
                    },
                backgroundDifficultyTint = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                triangles = new TrianglesV2
                {
                    ScaleAdjust = 1.2f,
                    Thickness = 0.01f,
                    Velocity = 0.3f,
                    RelativeSizeAxes = Axes.Both,
                    Alpha = CurrentInterfaceTheme.IsThemed() ? 0 : 1,
                },
                stableThumbContainer = new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(72f, 54f),
                    Margin = new MarginPadding { Left = 8f },
                    Masking = true,
                    CornerRadius = 3f,
                    Alpha = CurrentInterfaceTheme.IsThemed() ? 1 : 0,
                    Child = stableThumbBackground = new PanelSetBackground(),
                },
                leftRowFill = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Spacing = new Vector2(5),
                    Margin = new MarginPadding { Left = CurrentInterfaceTheme.IsThemed() ? 86f : 6.5f },
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 2),
                            Children = new Drawable[]
                            {
                                stableModeIcon = new Sprite
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Size = new Vector2(22),
                                    FillMode = FillMode.Fit,
                                    Alpha = CurrentInterfaceTheme.IsThemed() ? 1 : 0,
                                },
                                localRank = new PanelLocalRankDisplay
                                {
                                    Scale = new Vector2(0.8f),
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                },
                            },
                        },
                        mainFill = new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Direction = FillDirection.Vertical,
                            AutoSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Bottom = 3.5f },
                            Children = new Drawable[]
                            {
                                stableTitleText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                                    Alpha = CurrentInterfaceTheme.IsThemed() ? 1 : 0,
                                },
                                stableArtistText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Regular),
                                    Alpha = CurrentInterfaceTheme.IsThemed() ? 1 : 0,
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    AutoSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Bottom = 2 },
                                    Children = new Drawable[]
                                    {
                                        variantText = new OsuSpriteText
                                        {
                                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Alpha = 0,
                                        },
                                        difficultyText = new OsuSpriteText
                                        {
                                            Font = OsuFont.Style.Body.With(weight: FontWeight.Bold),
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
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(3),
                                    AutoSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        starRatingDisplay = new StarRatingDisplay(default, StarRatingDisplaySize.Small, animated: true)
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Scale = new Vector2(0.875f),
                                        },
                                        starCounter = new StarCounter
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Scale = new Vector2(0.4f)
                                        },
                                        stableStars = new StableStarRatingRow
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Alpha = 0,
                                        }
                                    },
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
        }

        protected override void ApplyInterfaceTheme()
        {
            base.ApplyInterfaceTheme();

            bool isStable = CurrentInterfaceTheme.IsThemed();
            bool isSelectedStable = isStable && Selected.Value;

            Color4 activeText = SkinManager?.GetSongSelectActiveTextColour() ?? Color4.Black;
            Color4 inactiveText = SkinManager?.GetSongSelectInactiveTextColour() ?? Color4.White;

            Height = isStable ? PanelBeatmapSet.HEIGHT : HEIGHT;
            leftRowFill.Margin = new MarginPadding { Left = isStable ? 86f : 6.5f };
            stableThumbContainer.Alpha = isStable ? 1 : 0;
            stableModeIcon.Alpha = isStable ? 1 : 0;
            stableModeIcon.Colour = isSelectedStable ? activeText : inactiveText;
            stableTitleText.Alpha = isStable ? 1 : 0;
            stableArtistText.Alpha = isStable ? 1 : 0;
            authorText.Alpha = isStable ? 0 : 1;

            if (contentBackground is Sprite s && SkinManager != null)
                s.Texture = SkinManager.GetClassicTexture(@"menu-button-background");

            lazerContentBackground.Alpha = isStable ? 0 : 1;
            contentBackground.Alpha = isStable ? 1 : 0;

            if (isSelectedStable)
                contentBackground.Colour = Color4.White;
            else
                contentBackground.Colour = CurrentInterfaceTheme.DifficultyRowBackground(ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4));

            triangles.Alpha = isStable ? 0 : 1;
            backgroundDifficultyTint.Alpha = isStable ? 0 : 1;

            stableTitleText.Colour = isSelectedStable ? activeText : inactiveText.Opacity(0.75f);
            stableArtistText.Colour = isSelectedStable ? activeText.Opacity(0.85f) : inactiveText.Opacity(0.75f);
            difficultyText.Colour = isSelectedStable ? activeText : (isStable ? inactiveText : Color4.White);
            variantText.Colour = isSelectedStable ? activeText : (isStable ? inactiveText : Color4.White);
            authorText.Colour = isSelectedStable
                ? activeText.Opacity(0.75f)
                : (isStable ? inactiveText.Opacity(0.85f) : colourProvider.Content2);

            starRatingDisplay.Alpha = isStable ? 0 : 1;
            starCounter.Alpha = isStable ? 0 : 1;
            stableStars.Alpha = isStable ? 1 : 0;
            stableStars.ActiveWhiteCard = isSelectedStable;
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            difficultyIcon.Icon = getRulesetIcon(beatmap.Ruleset);

            if (SkinManager != null)
            {
                string modeTex = beatmap.Ruleset.ShortName switch
                {
                    @"taiko" => @"mode-taiko-small",
                    @"fruits" => @"mode-fruits-small",
                    @"mania" => @"mode-mania-small",
                    _ => @"mode-osu-small",
                };
                // BeatmapTreeItem.cs lines 450-464 loads mode-*-small from SkinSource.Osu (internal default skin only)
                stableModeIcon.Texture = SkinManager.DefaultClassicSkin.GetTexture(modeTex);
            }

            scheduledBackgroundRetrieval = Scheduler.AddDelayed(b =>
            {
                stableThumbBackground.Beatmap = beatmaps.GetWorkingBeatmap(b);
            }, beatmap, 50);

            localRank.Beatmap = beatmap;
            stableTitleText.Text = new RomanisableString(beatmap.Metadata.TitleUnicode, beatmap.Metadata.Title);
            stableArtistText.Text = !string.IsNullOrEmpty(beatmap.Metadata.Author.Username)
                ? $"{beatmap.Metadata.Artist} // {beatmap.Metadata.Author.Username}"
                : new RomanisableString(beatmap.Metadata.ArtistUnicode, beatmap.Metadata.Artist);
            difficultyText.Text = beatmap.DifficultyName;
            authorText.Text = BeatmapsetsStrings.ShowDetailsMappedBy(beatmap.Metadata.Author.Username);

            computeStarRating();
            updateKeyCount();
            ApplyInterfaceTheme();
        }

        private Drawable getRulesetIcon(RulesetInfo rulesetInfo)
        {
            var rulesetInstance = rulesets.GetRuleset(rulesetInfo.ShortName)?.CreateInstance();

            if (rulesetInstance is null)
                return new SpriteIcon { Icon = FontAwesome.Regular.QuestionCircle };

            return rulesetInstance.CreateIcon();
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            scheduledBackgroundRetrieval?.Cancel();
            scheduledBackgroundRetrieval = null;
            stableThumbBackground.Beatmap = null;
            localRank.Beatmap = null;
            starDifficultyBindable = null;

            starDifficultyCancellationSource?.Cancel();
        }

        private void computeStarRating()
        {
            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource = new CancellationTokenSource();

            if (Item == null)
                return;

            var initialStored = new StarDifficulty(beatmap.StarRating, 0);
            stableStars.Stars = initialStored.Stars;

            // Potato mode: carousel rows use the stored nomod star rating instead of recalculating
            // per panel while scrolling (the wedge still shows mod-adjusted difficulty).
            if (PotatoMode.Active)
            {
                starRatingDisplay.Current.Value = initialStored;
                starCounter.Current = (float)initialStored.Stars;
                return;
            }

            starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmap, starDifficultyCancellationSource.Token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            starDifficultyBindable.BindValueChanged(starDifficulty =>
            {
                starRatingDisplay.Current.Value = starDifficulty.NewValue;
                starCounter.Current = (float)starDifficulty.NewValue.Stars;
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

            var diffColour = starRatingDisplay.DisplayedDifficultyColour;

            if (AccentColour != diffColour)
            {
                AccentColour = diffColour;
                starCounter.Colour = diffColour;

                backgroundBorder.Colour = diffColour;
                backgroundDifficultyTint.Colour = ColourInfo.GradientHorizontal(diffColour.Opacity(0.25f), diffColour.Opacity(0f));

                triangles.Colour = ColourInfo.GradientVertical(diffColour.Opacity(0.25f), diffColour.Opacity(0f));
            }

            if (difficultyIcon.Colour != starRatingDisplay.DisplayedDifficultyTextColour)
            {
                difficultyIcon.Colour = starRatingDisplay.DisplayedDifficultyTextColour;
            }
        }

        private void updateKeyCount()
        {
            if (Item == null)
                return;

            var rulesetInstance = ruleset.Value.CreateInstance();

            if (rulesetInstance.GameplayVariants.Count() > 1)
            {
                int variant = rulesetInstance.GetVariantForBeatmap(beatmap, mods.Value);
                var variantName = rulesetInstance.GetVariantName(variant);

                variantText.Alpha = 1;
                variantText.Text = LocalisableString.Interpolate($"[{variantName}] ");
            }
            else
                variantText.Alpha = 0;
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
