// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Screens.Select
{
    public partial class PanelBeatmapSet : Panel
    {
        public const float HEIGHT = CarouselItem.DEFAULT_HEIGHT * 1.6f;

        public Bindable<HashSet<BeatmapInfo>?> VisibleBeatmaps { get; } = new Bindable<HashSet<BeatmapInfo>?>();

        private Box chevronBackground = null!;
        private Drawable themeTintBox = null!;
        private PanelSetBackground setBackground = null!;
        private Container stableThumbContainer = null!;
        private PanelSetBackground stableThumbBackground = null!;
        private FillFlowContainer mainTextFlow = null!;
        private FillFlowContainer badgesFlow = null!;
        private ScheduledDelegate? scheduledBackgroundRetrieval;

        private OsuSpriteText titleText = null!;
        private OsuSpriteText artistText = null!;
        private Drawable chevronIcon = null!;
        private PanelUpdateBeatmapButton updateButton = null!;
        private BeatmapSetOnlineStatusPill statusPill = null!;
        private SpreadDisplay spreadDisplay = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private BeatmapSetOverlay? beatmapOverlay { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        private GroupedBeatmapSet groupedBeatmapSet
        {
            get
            {
                Debug.Assert(Item != null);
                return (GroupedBeatmapSet)Item!.Model;
            }
        }

        public PanelBeatmapSet()
        {
            PanelXOffset = 20f;
        }

        [BackgroundDependencyLoader]
        private void load(SkinManager skinManager)
        {
            Height = HEIGHT;
            var menuButtonBgTex = skinManager.GetClassicTexture(@"menu-button-background");

            Icon = chevronIcon = new Container
            {
                Size = new Vector2(0, 22),
                Child = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = FontAwesome.Solid.ChevronRight,
                    Size = new Vector2(8),
                    X = 1f,
                    Colour = colourProvider.Background5,
                },
            };

            Background = chevronBackground = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
                Alpha = 0f,
            };

            Content.Children = new Drawable[]
            {
                // osu!stable draws no full-width set background, only the thumbnail (stableThumbContainer);
                // a skin's semi-transparent menu-button-background would otherwise show lazer's one through it.
                setBackground = new PanelSetBackground { Alpha = CurrentInterfaceTheme.IsThemed() ? 0 : 1 },
                themeTintBox = menuButtonBgTex != null
                    ? new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(1.02f, 1.22f),
                        FillMode = FillMode.Stretch,
                        Texture = menuButtonBgTex,
                        Colour = CurrentInterfaceTheme.SetCardTint(),
                        Alpha = CurrentInterfaceTheme.IsThemed() ? 0.94f : 0,
                    }
                    : new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = CurrentInterfaceTheme.SetCardTint(),
                        Alpha = CurrentInterfaceTheme.IsThemed() ? 0.88f : 0,
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
                    Child = stableThumbBackground = new PanelSetBackground { StableThumbnail = true },
                },
                mainTextFlow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Padding = new MarginPadding { Top = 7.5f, Left = CurrentInterfaceTheme.IsThemed() ? 88f : 15f, Bottom = 13 },
                    Children = new Drawable[]
                    {
                        titleText = new OsuSpriteText
                        {
                            Font = OsuFont.Style.Heading1.With(typeface: Typeface.TorusAlternate),
                        },
                        artistText = new OsuSpriteText
                        {
                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                        },
                        badgesFlow = new FillFlowContainer
                        {
                            Direction = FillDirection.Horizontal,
                            AutoSizeAxes = Axes.Both,
                            Margin = new MarginPadding { Top = 4f },
                            Alpha = CurrentInterfaceTheme.IsThemed() ? 0 : 1,
                            Children = new Drawable[]
                            {
                                statusPill = new BeatmapSetOnlineStatusPill
                                {
                                    Origin = Anchor.CentreLeft,
                                    Anchor = Anchor.CentreLeft,
                                    TextSize = OsuFont.Style.Caption2.Size,
                                    Margin = new MarginPadding { Right = 5f },
                                    Animated = false,
                                },
                                updateButton = new PanelUpdateBeatmapButton
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Margin = new MarginPadding { Right = 5f, Top = -2f },
                                },
                                spreadDisplay = new SpreadDisplay
                                {
                                    Origin = Anchor.CentreLeft,
                                    Anchor = Anchor.CentreLeft,
                                    VisibleBeatmaps = { BindTarget = VisibleBeatmaps },
                                },
                            },
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Expanded.BindValueChanged(_ => onExpanded(), true);
            KeyboardSelected.BindValueChanged(k => KeyboardSelected.Value = k.NewValue, true);
        }

        protected override void ApplyInterfaceTheme()
        {
            base.ApplyInterfaceTheme();

            bool themed = CurrentInterfaceTheme.IsThemed();
            if (themeTintBox is Sprite s && SkinManager != null)
                s.Texture = SkinManager.GetClassicTexture(@"menu-button-background");

            if (themed && CurrentInterfaceTheme == InterfaceTheme.OsuStable && Item != null)
            {
                bool hasBeenPlayed = groupedBeatmapSet.BeatmapSet.Beatmaps.Any(b => b.LastPlayed != null);
                themeTintBox.Colour = hasBeenPlayed
                    ? osu.Framework.Extensions.Color4Extensions.Color4Extensions.FromHex(@"E96800")
                    : osu.Framework.Extensions.Color4Extensions.Color4Extensions.FromHex(@"EB4999");
            }
            else
            {
                themeTintBox.Colour = CurrentInterfaceTheme.SetCardTint();
            }

            themeTintBox.Alpha = themed ? 0.94f : 0;
            setBackground.Alpha = themed ? 0 : 1;
            stableThumbContainer.Alpha = themed ? 1 : 0;
            badgesFlow.Alpha = themed ? 0 : 1;

            Color4 inactiveText = themed ? (SkinManager?.GetSongSelectInactiveTextColour() ?? Color4.White) : Color4.White;
            titleText.Colour = inactiveText;
            artistText.Colour = inactiveText;

            mainTextFlow.Padding = new MarginPadding
            {
                Top = themed ? 6f : 7.5f,
                Left = themed ? StableRowOffset(themeTintBox, 75f, 88f) : 15f,
                Bottom = themed ? 6f : 13f,
            };

            if (Item != null)
                updateArtistText(groupedBeatmapSet.BeatmapSet);
        }

        protected override void Update()
        {
            base.Update();

            if (!CurrentInterfaceTheme.IsThemed())
            {
                if (mainTextFlow.Scale.X != 1)
                    mainTextFlow.Scale = Vector2.One;
                return;
            }

            FitStableTextBlock(mainTextFlow, 0.9f);

            // osu!stable's offsets on menu-button-background, following the image's drawn width (see StableRowOffset)
            float textLeft = StableRowOffset(themeTintBox, 75f, 88f);
            float thumbLeft = StableRowOffset(themeTintBox, 5.2f, 8f);

            if (Math.Abs(mainTextFlow.Padding.Left - textLeft) > 0.5f)
                mainTextFlow.Padding = mainTextFlow.Padding with { Left = textLeft };
            if (Math.Abs(stableThumbContainer.Margin.Left - thumbLeft) > 0.5f)
                stableThumbContainer.Margin = new MarginPadding { Left = thumbLeft };
        }

        private void updateArtistText(BeatmapSetInfo beatmapSet)
        {
            if (CurrentInterfaceTheme.IsThemed() && !string.IsNullOrEmpty(beatmapSet.Metadata.Author.Username))
            {
                artistText.Text = $"{beatmapSet.Metadata.Artist} // {beatmapSet.Metadata.Author.Username}";
            }
            else
            {
                artistText.Text = new RomanisableString(beatmapSet.Metadata.ArtistUnicode, beatmapSet.Metadata.Artist);
            }
        }

        private void onExpanded()
        {
            if (Expanded.Value)
            {
                chevronBackground.FadeIn(DURATION / 2, Easing.OutQuint);
                chevronIcon.ResizeWidthTo(18, DURATION * 1.5f, Easing.OutElasticQuarter);
                chevronIcon.FadeTo(1f, DURATION, Easing.OutQuint);
            }
            else
            {
                chevronBackground.FadeOut(DURATION, Easing.OutQuint);
                chevronIcon.ResizeWidthTo(0f, DURATION, Easing.OutQuint);
                chevronIcon.FadeTo(0f, DURATION, Easing.OutQuint);
            }

            spreadDisplay.Expanded.Value = Expanded.Value;
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            var beatmapSet = groupedBeatmapSet.BeatmapSet;

            // Choice of background image matches BSS implementation (always uses the lowest `beatmap_id` from the set).
            scheduledBackgroundRetrieval = Scheduler.AddDelayed(s =>
            {
                var working = beatmaps.GetWorkingBeatmap(s.Beatmaps.MinBy(b => b.OnlineID));
                setBackground.Beatmap = working;
                stableThumbBackground.Beatmap = working;
            }, beatmapSet, 50);

            titleText.Text = new RomanisableString(beatmapSet.Metadata.TitleUnicode, beatmapSet.Metadata.Title);
            updateArtistText(beatmapSet);
            updateButton.BeatmapSet = beatmapSet;
            statusPill.Status = beatmapSet.Status;
            spreadDisplay.BeatmapSet.Value = beatmapSet;
            ApplyInterfaceTheme();
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            scheduledBackgroundRetrieval?.Cancel();
            scheduledBackgroundRetrieval = null;
            setBackground.Beatmap = null;
            stableThumbBackground.Beatmap = null;
            updateButton.BeatmapSet = null;
            spreadDisplay.BeatmapSet.Value = null;
        }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

        public override MenuItem[] ContextMenuItems
        {
            get
            {
                if (Item == null)
                    return Array.Empty<MenuItem>();

                var beatmapSet = groupedBeatmapSet.BeatmapSet;

                List<MenuItem> items = new List<MenuItem>();

                if (Expanded.Value)
                {
                    if (songSelect is SoloSongSelect soloSongSelect)
                    {
                        // Assume the current set has one of its beatmaps selected since it is expanded.
                        items.Add(new OsuMenuItem(ButtonSystemStrings.Edit.ToSentence(), MenuItemType.Standard, () => soloSongSelect.Edit(soloSongSelect.Beatmap.Value.BeatmapInfo))
                        {
                            Icon = FontAwesome.Solid.PencilAlt
                        });
                        items.Add(new OsuMenuItemSpacer());
                    }
                }
                else
                {
                    items.Add(new OsuMenuItem(WebCommonStrings.ButtonsExpand.ToSentence(), MenuItemType.Highlighted, () => TriggerClick()));
                    items.Add(new OsuMenuItemSpacer());
                }

                if (beatmapSet.OnlineID > 0)
                {
                    items.Add(new OsuMenuItem(CommonStrings.Details, MenuItemType.Standard, () => beatmapOverlay?.FetchAndShowBeatmapSet(beatmapSet.OnlineID)));

                    if (beatmapSet.GetOnlineURL(api, ruleset.Value) is string url)
                        items.Add(new OsuMenuItem(CommonStrings.CopyLink, MenuItemType.Standard, () => game?.CopyToClipboard(url)));

                    items.Add(new OsuMenuItemSpacer());
                }

                var collectionItems = realm.Realm.All<BeatmapCollection>()
                                           .OrderBy(c => c.Name)
                                           .AsEnumerable()
                                           .Select(createCollectionMenuItem)
                                           .ToList();

                if (manageCollectionsDialog != null)
                    collectionItems.Add(new OsuMenuItem(CommonStrings.Manage, MenuItemType.Standard, manageCollectionsDialog.Show));

                items.Add(new OsuMenuItem(CommonStrings.Collections) { Items = collectionItems });

                if (beatmapSet.Beatmaps.Any(b => b.Hidden))
                    items.Add(new OsuMenuItem(SongSelectStrings.RestoreAllHidden, MenuItemType.Standard, () => songSelect?.RestoreAllHidden(beatmapSet)));

                items.Add(new OsuMenuItem(CommonStrings.DeleteWithConfirmation, MenuItemType.Destructive, () => songSelect?.Delete(beatmapSet)));
                return items.ToArray();
            }
        }

        private MenuItem createCollectionMenuItem(BeatmapCollection collection)
        {
            var beatmapSet = groupedBeatmapSet.BeatmapSet;

            TernaryState state;

            int countExisting = beatmapSet.Beatmaps.Count(b => collection.BeatmapMD5Hashes.Contains(b.MD5Hash));

            if (countExisting == beatmapSet.Beatmaps.Count)
                state = TernaryState.True;
            else if (countExisting > 0)
                state = TernaryState.Indeterminate;
            else
                state = TernaryState.False;

            var liveCollection = collection.ToLive(realm);

            return new TernaryStateToggleMenuItem(collection.Name, MenuItemType.Standard, s =>
            {
                Task.Run(() => liveCollection.PerformWrite(c =>
                {
                    foreach (var b in beatmapSet.Beatmaps)
                    {
                        switch (s)
                        {
                            case TernaryState.True:
                                if (c.BeatmapMD5Hashes.Contains(b.MD5Hash))
                                    continue;

                                c.BeatmapMD5Hashes.Add(b.MD5Hash);
                                break;

                            case TernaryState.False:
                                c.BeatmapMD5Hashes.Remove(b.MD5Hash);
                                break;
                        }
                    }
                }));
            })
            {
                State = { Value = state }
            };
        }
    }
}
