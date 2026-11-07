// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.Chat;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// osu!stable Song Select top bar with 5-line beatmap metadata on the left
    /// and Group / Sort / Search controls on the right.
    /// </summary>
    public partial class StableSongSelectHeader : VisibilityContainer
    {
        public const float HEIGHT = 104f;
        public const float LEFT_COLUMN_TOP_HEIGHT = 128f;

        private readonly FilterControl filterControl;

        [Resolved]
        private IBindable<WorkingBeatmap> working { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private ILinkHandler? linkHandler { get; set; }

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private IBindable<Skin> currentSkin = null!;
        private Sprite? topSprite;
        private Container widescreenTopExtension = null!;
        private Container tabsContainer = null!;
        private Sprite statusIcon = null!;
        private OsuSpriteText line1Title = null!;
        private OsuSpriteText line2Mapper = null!;
        private OsuHoverContainer mapperHover = null!;
        private OsuSpriteText line3LengthBpm = null!;
        private OsuSpriteText line4Objects = null!;
        private OsuSpriteText line5Attributes = null!;

        private StableSearchTextBox searchBox = null!;
        private OsuSpriteText searchMatchCountText = null!;
        private StableSimpleDropdown<GroupMode> groupDropdown = null!;
        private StableSimpleDropdown<SortMode> sortDropdown = null!;
        private readonly List<StableGroupingTab> groupingTabs = new List<StableGroupingTab>();

        private IBindable<StarDifficulty>? starDifficultyBindable;
        private CancellationTokenSource? cancellationSource;
        private ModSettingChangeTracker? settingChangeTracker;

        private static readonly Color4 colour_harder = Color4Extensions.FromHex(@"F69AA1");
        private static readonly Color4 colour_easier = Color4Extensions.FromHex(@"ADD8E6");
        private static readonly Color4 colour_group_label = Color4Extensions.FromHex(@"92C3E6");
        private static readonly Color4 colour_sort_label = Color4Extensions.FromHex(@"AED28B");
        private static readonly Color4 colour_search_label = Color4Extensions.FromHex(@"ADFF2F");

        public StableSongSelectHeader(FilterControl filterControl)
        {
            this.filterControl = filterControl;
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            var topTexture = skinManager.GetClassicTexture(@"songselect-top");
            var tabTexture = skinManager.GetClassicTexture(@"selection-tab");

            var tabDefinitions = new (string Label, GroupMode Mode)[]
            {
                ("Collections", GroupMode.Collections),
                ("Recently Played", GroupMode.LastPlayed),
                ("By Artist", GroupMode.Artist),
                ("By Difficulty", GroupMode.Difficulty),
                ("No Grouping", GroupMode.None),
            };

            tabsContainer = new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                RelativeSizeAxes = Axes.X,
                Height = HEIGHT,
            };

            float tabRightOffset = 118f;
            foreach (var (label, mode) in tabDefinitions)
            {
                var tab = new StableGroupingTab(tabTexture, label, mode, m => groupDropdown.Current.Value = m)
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.BottomLeft,
                    Position = new Vector2(-tabRightOffset, 78f),
                };
                groupingTabs.Add(tab);
                tabsContainer.Add(tab);
                tabRightOffset += 102f;
            }

            InternalChildren = new Drawable[]
            {
                // Widescreen extension for screens wider than 1366px
                widescreenTopExtension = new Container
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 81f,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black,
                        },
                        new Box
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.X,
                            Height = 3f,
                            Colour = new Color4(49, 94, 237, 255),
                        },
                    },
                },
                topTexture != null
                    ? (topSprite = new Sprite
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Texture = topTexture,
                    })
                    : new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.9f),
                    },
                // Beatmap ranked status icon at (12, 12) * 1.6f = (19.2, 19.2)
                statusIcon = new Sprite
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.Centre,
                    Position = new Vector2(19.2f, 19.2f),
                    Scale = new Vector2(0.8f),
                },
                // Top-left 5-line beatmap metadata block at exact osu!stable coordinates
                line1Title = new OsuSpriteText
                {
                    Position = new Vector2(36f, 1f),
                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Regular),
                    Colour = Color4.White,
                    Shadow = true,
                },
                mapperHover = new OsuHoverContainer
                {
                    Position = new Vector2(38f, 22f),
                    AutoSizeAxes = Axes.Both,
                    Child = line2Mapper = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                        Colour = Color4.White,
                        Shadow = true,
                    },
                },
                line3LengthBpm = new OsuSpriteText
                {
                    Position = new Vector2(6f, 40f),
                    Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                    Colour = Color4.White,
                    Shadow = true,
                },
                line4Objects = new OsuSpriteText
                {
                    Position = new Vector2(6f, 58f),
                    Font = OsuFont.GetFont(size: 13, weight: FontWeight.Regular),
                    Colour = Color4.White.Opacity(0.95f),
                    Shadow = true,
                },
                line5Attributes = new OsuSpriteText
                {
                    Position = new Vector2(6f, 75f),
                    Font = OsuFont.GetFont(size: 13.5f, weight: FontWeight.Regular),
                    Colour = Color4.White,
                    Shadow = true,
                },
                // Red angled selection-tab sprites along the top-right lip
                tabsContainer,
                // Top-right Group / Sort dropdowns
                new FillFlowContainer
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Margin = new MarginPadding { Right = 16, Top = 8 },
                    Spacing = new Vector2(10, 0),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = "Group",
                            Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                            Colour = colour_group_label,
                            Shadow = true,
                        },
                        groupDropdown = new StableSimpleDropdown<GroupMode>
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Width = 148,
                            Items = Enum.GetValues<GroupMode>(),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = "Sort",
                            Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                            Colour = colour_sort_label,
                            Shadow = true,
                        },
                        sortDropdown = new StableSimpleDropdown<SortMode>
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Width = 148,
                            Items = Enum.GetValues<SortMode>(),
                        },
                    },
                },
                // Search bar box directly under the top-right lip (like osu!stable's searchBackground)
                new Container
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(0f, 81f),
                    Width = 360f,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.55f),
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Horizontal = 10, Vertical = 3 },
                            Spacing = new Vector2(0, 1),
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(4, 0),
                                    Children = new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Text = "Search:",
                                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                                            Colour = colour_search_label,
                                            Shadow = true,
                                        },
                                        searchBox = new StableSearchTextBox
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Size = new Vector2(260, 20),
                                        },
                                    },
                                },
                                searchMatchCountText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                                    Colour = Color4.White,
                                    Shadow = true,
                                },
                            },
                        },
                    },
                },
            };

            config.BindWith(OsuSetting.SongSelectGroupMode, groupDropdown.Current);
            config.BindWith(OsuSetting.SongSelectSortingMode, sortDropdown.Current);

            groupDropdown.Current.BindValueChanged(g =>
            {
                foreach (var tab in groupingTabs)
                    tab.SetActive(tab.Mode == g.NewValue);
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            searchBox.Current.BindValueChanged(e => filterControl.Search(e.NewValue));

            working.BindValueChanged(_ => Scheduler.AddOnce(updateMetadata));
            ruleset.BindValueChanged(_ => Scheduler.AddOnce(updateMetadata));

            currentSkin = skinManager.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ =>
            {
                if (topSprite != null)
                    topSprite.Texture = skinManager.GetClassicTexture(@"songselect-top");

                var tabTex = skinManager.GetClassicTexture(@"selection-tab");
                foreach (var tab in groupingTabs)
                    tab.UpdateTexture(tabTex);

                Scheduler.AddOnce(updateMetadata);
            });

            mods.BindValueChanged(m =>
            {
                settingChangeTracker?.Dispose();
                Scheduler.AddOnce(updateMetadata);

                if (m.NewValue.Any())
                {
                    settingChangeTracker = new ModSettingChangeTracker(m.NewValue);
                    settingChangeTracker.SettingChanged += _ => Scheduler.AddOnce(updateMetadata);
                }
            }, true);

            updateMetadata();
        }

        public void SetMatchCountText(LocalisableString text)
        {
            searchMatchCountText.Text = text;
        }

        protected override void PopIn() => this.FadeIn(200, Easing.OutQuint);
        protected override void PopOut() => this.FadeOut(200, Easing.OutQuint);

        private double currentStars;

        private void updateMetadata()
        {
            cancellationSource?.Cancel();
            cancellationSource = new CancellationTokenSource();
            var token = cancellationSource.Token;

            var beatmapInfo = working.Value.BeatmapInfo;
            var meta = working.Value.Metadata;

            string statusTexName = beatmapInfo.Status switch
            {
                BeatmapOnlineStatus.Ranked => @"selection-ranked",
                BeatmapOnlineStatus.Approved or BeatmapOnlineStatus.Qualified => @"selection-approved",
                BeatmapOnlineStatus.Loved => @"selection-loved",
                _ => @"selection-question",
            };
            statusIcon.Texture = skinManager.GetClassicTexture(statusTexName);

            var artist = new RomanisableString(meta.ArtistUnicode, meta.Artist);
            var title = new RomanisableString(meta.TitleUnicode, meta.Title);

            line1Title.Text = LocalisableString.Interpolate($"{artist} - {title} [{beatmapInfo.DifficultyName}]");
            line2Mapper.Text = $"Mapped by {meta.Author.Username}";
            mapperHover.Action = () => linkHandler?.HandleLink(new LinkDetails(LinkAction.OpenUserProfile, meta.Author));

            starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmapInfo, token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            starDifficultyBindable.BindValueChanged(d =>
            {
                currentStars = d.NewValue.Stars;
                updateLine5Attributes();
            }, true);

            double rate = ModUtils.CalculateRateWithMods(mods.Value);
            line3LengthBpm.Colour = Precision.AlmostEquals(rate, 1.0)
                ? Color4.White
                : (rate > 1.0 ? colour_harder : colour_easier);

            Task.Run(() =>
            {
                var b = working.Value.Beatmap;
                var playable = working.Value.GetPlayableBeatmap(ruleset.Value);

                int bpmMax = FormatUtils.RoundBPM(b.ControlPointInfo.BPMMaximum, rate);
                int bpmMin = FormatUtils.RoundBPM(b.ControlPointInfo.BPMMinimum, rate);
                double hitLength = Math.Round(beatmapInfo.Length / rate);
                int totalObjects = playable.HitObjects.Count;

                var stats = playable.GetStatistics().ToList();
                string statsSummary = string.Join("  ", stats.Select(s => $"{s.Name}: {s.Content}"));

                Schedule(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    string bpmStr = bpmMin == bpmMax ? $"{bpmMin}" : $"{bpmMin}-{bpmMax}";
                    line3LengthBpm.Text = $"Length: {hitLength.ToFormattedDuration()} BPM: {bpmStr} Objects: {totalObjects:N0}";
                    line4Objects.Text = statsSummary;
                });
            }, token);

            updateLine5Attributes();
        }

        private void updateLine5Attributes()
        {
            var beatmapInfo = working.Value.BeatmapInfo;
            var baseDiff = beatmapInfo.Difficulty;

            var rulesetInstance = ruleset.Value.CreateInstance();
            var adjustedDiff = rulesetInstance.GetAdjustedDisplayDifficulty(beatmapInfo, mods.Value);

            line5Attributes.Text = $"CS:{adjustedDiff.CircleSize:0.##} AR:{adjustedDiff.ApproachRate:0.##} OD:{adjustedDiff.OverallDifficulty:0.##} HP:{adjustedDiff.DrainRate:0.##} Star Rating: {currentStars:0.00}★";

            bool harder = adjustedDiff.OverallDifficulty > baseDiff.OverallDifficulty + 0.05f || adjustedDiff.ApproachRate > baseDiff.ApproachRate + 0.05f;
            bool easier = adjustedDiff.OverallDifficulty < baseDiff.OverallDifficulty - 0.05f || adjustedDiff.ApproachRate < baseDiff.ApproachRate - 0.05f;

            line5Attributes.Colour = harder ? colour_harder : (easier ? colour_easier : Color4.White);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            cancellationSource?.Cancel();
            settingChangeTracker?.Dispose();
        }

        private partial class StableSearchTextBox : OsuTextBox
        {
            public StableSearchTextBox()
            {
                PlaceholderText = "Type to search!";
                CornerRadius = 0;
                BorderThickness = 0;
                FontSize = 16;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                BackgroundUnfocused = Color4.Transparent;
                BackgroundFocused = Color4.Transparent;
                BackgroundCommit = Color4.Transparent;
                BorderThickness = 0;
            }

            protected override void OnFocus(FocusEvent e)
            {
                base.OnFocus(e);
                BorderThickness = 0;
            }

            protected override void OnFocusLost(FocusLostEvent e)
            {
                base.OnFocusLost(e);
                BorderThickness = 0;
            }
        }

        private partial class StableSimpleDropdown<T> : OsuDropdown<T>
            where T : struct, Enum
        {
            protected override DropdownHeader CreateHeader() => new StableDropdownHeader();

            protected override LocalisableString GenerateItemText(T item) => item.GetLocalisableDescription();

            private partial class StableDropdownHeader : OsuDropdownHeader
            {
                public StableDropdownHeader()
                {
                    CornerRadius = 3;
                    Height = 24;
                    Foreground.Padding = new MarginPadding { Top = 3, Bottom = 3, Left = 8, Right = 8 };
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    BackgroundColour = Color4Extensions.FromHex(@"1C2430").Opacity(0.95f);
                    BackgroundColourHover = Color4Extensions.FromHex(@"2A394D");
                }
            }
        }

        private partial class StableGroupingTab : OsuClickableContainer
        {
            public readonly GroupMode Mode;
            private readonly Texture? tabTexture;
            private readonly string label;

            private Drawable tabBg = null!;
            private OsuSpriteText tabText = null!;
            private bool isActive;

            public StableGroupingTab(Texture? tabTexture, string label, GroupMode mode, Action<GroupMode> onSelect)
            {
                this.tabTexture = tabTexture;
                this.label = label;
                Mode = mode;

                Action = () => onSelect(mode);
                Size = new Vector2(99f, 17f);
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    tabBg = tabTexture != null
                        ? new Sprite
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.Both,
                            Size = Vector2.One,
                            FillMode = FillMode.Stretch,
                            Texture = tabTexture,
                            Colour = Color4Extensions.FromHex(@"D62222"),
                        }
                        : new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4Extensions.FromHex(@"D62222"),
                        },
                    tabText = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Y = 1f,
                        Text = label,
                        Font = OsuFont.GetFont(size: 11.5f, weight: FontWeight.Bold),
                        Colour = Color4.White,
                        Shadow = true,
                    },
                };
            }

            public void SetActive(bool active)
            {
                isActive = active;
                if (tabBg != null)
                    tabBg.Colour = active ? Color4.White : Color4Extensions.FromHex(@"D62222");
                if (tabText != null)
                    tabText.Colour = active ? Color4.Black : Color4.White;
            }

            public void UpdateTexture(Texture? newTex)
            {
                if (tabBg is Sprite s && newTex != null)
                    s.Texture = newTex;
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (!isActive)
                    tabBg.FadeColour(Color4Extensions.FromHex(@"FF4444"), 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (!isActive)
                    tabBg.FadeColour(Color4Extensions.FromHex(@"D62222"), 100);
                base.OnHoverLost(e);
            }
        }
    }
}
