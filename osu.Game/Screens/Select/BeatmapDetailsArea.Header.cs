// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.Leaderboards;
using osu.Game.Extensions;
using osu.Game.Rulesets;
using osu.Game.Screens.Play.Leaderboards;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapDetailsArea
    {
        public partial class Header : CompositeDrawable
        {
            private WedgeSelector<Selection> tabControl = null!;
            private FillFlowContainer leaderboardControls = null!;
            private Container lazerHeaderContainer = null!;
            private FillFlowContainer stableHeaderContainer = null!;

            private ShearedDropdown<BeatmapLeaderboardScope> scopeDropdown = null!;
            private ShearedDropdown<LeaderboardSortMode> sortDropdown = null!;
            private VariantDropdown variantDropdown = null!;
            private ShearedToggleButton selectedModsToggle = null!;

            private StableRankingModeDropdown stableRankingDropdown = null!;
            private StableCyanDropdown<LeaderboardVariant> stableVariantDropdown = null!;
            private StableCyanDropdown<LeaderboardSortMode> stableSortDropdown = null!;
            private IBindable<InterfaceTheme> interfaceTheme = null!;
            private bool syncingStableRanking;

            [Resolved]
            private IBindable<RulesetInfo> ruleset { get; set; } = null!;

            public IBindable<Selection> Type => tabControl.Current;

            public IBindable<BeatmapLeaderboardScope> Scope => scopeDropdown.Current;

            public IBindable<LeaderboardVariant> Variant => variantDropdown.Current;

            private readonly Bindable<BeatmapDetailTab> configDetailTab = new Bindable<BeatmapDetailTab>();

            public IBindable<LeaderboardSortMode> Sorting => sortDropdown.Current;

            private readonly Bindable<LeaderboardSortMode> configLeaderboardSortMode = new Bindable<LeaderboardSortMode>();

            public IBindable<bool> FilterBySelectedMods => selectedModsToggle.Active;

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config)
            {
                interfaceTheme = config.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);

                InternalChildren = new Drawable[]
                {
                    lazerHeaderContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = SongSelect.WEDGE_CONTENT_MARGIN, Right = 5f },
                        Children = new Drawable[]
                        {
                            tabControl = new WedgeSelector<Selection>(20f)
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 200,
                                Height = 22,
                                Margin = new MarginPadding { Top = 2f },
                                IsSwitchable = true,
                            },
                            leaderboardControls = new FillFlowContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                RelativeSizeAxes = Axes.X,
                                Height = 30,
                                Spacing = new Vector2(5f, 0f),
                                Direction = FillDirection.Horizontal,
                                Padding = new MarginPadding { Left = 258 },
                                Children = new Drawable[]
                                {
                                    selectedModsToggle = new ShearedToggleButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        AutoSizeAxes = Axes.X,
                                        Text = UserInterfaceStrings.SelectedMods,
                                        Height = 30f,
                                        // Eyeballed to make spacing match. Because shear is silly and implemented in different ways between dropdown and button.
                                        Margin = new MarginPadding { Left = -9.2f },
                                    },
                                    variantDropdown = new VariantDropdown
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.X,
                                        Width = 0.3f,
                                    },
                                    sortDropdown = new ShearedDropdown<LeaderboardSortMode>(BeatmapLeaderboardWedgeStrings.Sort)
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.X,
                                        Width = 0.3f,
                                        Items = Enum.GetValues<LeaderboardSortMode>(),
                                    },
                                    scopeDropdown = new ScopeDropdown
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.X,
                                        Width = 0.3f,
                                        Current = { Value = BeatmapLeaderboardScope.Global },
                                    },
                                },
                            },
                        },
                    },
                    stableHeaderContainer = new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(6f, 0f),
                        Padding = new MarginPadding { Left = 4f },
                        Alpha = 0f,
                        Children = new Drawable[]
                        {
                            stableRankingDropdown = new StableRankingModeDropdown
                            {
                                // the three fit on songselect-top's black ledge, left of its curve (about 360 wide)
                                Width = 150f,
                            },
                            stableVariantDropdown = new StableCyanDropdown<LeaderboardVariant>
                            {
                                Width = 84f,
                                Items = new[] { LeaderboardVariant.Vanilla },
                                Current = { BindTarget = variantDropdown.Current },
                            },
                            stableSortDropdown = new StableCyanDropdown<LeaderboardSortMode>
                            {
                                Width = 100f,
                                Items = Enum.GetValues<LeaderboardSortMode>(),
                                Current = { BindTarget = sortDropdown.Current },
                            },
                        },
                    },
                };

                config.BindWith(OsuSetting.BeatmapDetailTab, configDetailTab);
                config.BindWith(OsuSetting.BeatmapLeaderboardSortMode, configLeaderboardSortMode);
                config.BindWith(OsuSetting.BeatmapDetailModsFilter, selectedModsToggle.Active);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                scopeDropdown.Current.Value = tryMapDetailTabToLeaderboardScope(configDetailTab.Value) ?? scopeDropdown.Current.Value;
                scopeDropdown.Current.BindValueChanged(_ => updateConfigDetailTab());

                tabControl.Current.Value = configDetailTab.Value == BeatmapDetailTab.Details ? Selection.Details : Selection.Ranking;
                tabControl.Current.BindValueChanged(v =>
                {
                    leaderboardControls.FadeTo(v.NewValue == Selection.Ranking ? 1 : 0, 300, Easing.OutQuint);
                    updateConfigDetailTab();
                }, true);

                ruleset.BindValueChanged(r =>
                {
                    updateVariantOptions(r.NewValue);
                    // TumThai: a custom ruleset's boards are by score only.
                    if (scopeDropdown.Current.Value != BeatmapLeaderboardScope.Local)
                    {
                        sortDropdown.Items = onlineSortModes(variantDropdown.Current.Value);
                        sortDropdown.Current.Value = defaultOnlineSort(variantDropdown.Current.Value);
                    }
                }, true);

                scopeDropdown.Current.BindValueChanged(scope =>
                {
                    if (scope.NewValue == BeatmapLeaderboardScope.Local)
                    {
                        sortDropdown.Items = Enum.GetValues<LeaderboardSortMode>();
                        sortDropdown.Current.BindTo(configLeaderboardSortMode);
                    }
                    else
                    {
                        // osu!somtum: online boards are ordered by the server, by pp or by score (Relax/Autopilot: pp only).
                        sortDropdown.Current.UnbindFrom(configLeaderboardSortMode);
                        sortDropdown.Items = onlineSortModes(variantDropdown.Current.Value);
                        sortDropdown.Current.Value = defaultOnlineSort(variantDropdown.Current.Value);
                    }
                }, true);

                // Each online board starts in its own order: pp for Relax/Autopilot, score for vanilla.
                variantDropdown.Current.BindValueChanged(variant =>
                {
                    if (scopeDropdown.Current.Value != BeatmapLeaderboardScope.Local)
                    {
                        var modes = onlineSortModes(variant.NewValue);
                        sortDropdown.Items = modes;
                        stableSortDropdown.Items = modes;
                        sortDropdown.Current.Value = defaultOnlineSort(variant.NewValue);
                    }
                });

                scopeDropdown.Current.BindValueChanged(_ => syncToStableRankingDropdown());
                selectedModsToggle.Active.BindValueChanged(_ => syncToStableRankingDropdown(), true);

                stableRankingDropdown.Current.BindValueChanged(e =>
                {
                    if (syncingStableRanking)
                        return;

                    syncingStableRanking = true;
                    switch (e.NewValue)
                    {
                        case StableRankingMode.Local:
                            selectedModsToggle.Active.Value = false;
                            scopeDropdown.Current.Value = BeatmapLeaderboardScope.Local;
                            break;

                        case StableRankingMode.Global:
                            selectedModsToggle.Active.Value = false;
                            scopeDropdown.Current.Value = BeatmapLeaderboardScope.Global;
                            break;

                        case StableRankingMode.SelectedMods:
                            scopeDropdown.Current.Value = BeatmapLeaderboardScope.Global;
                            selectedModsToggle.Active.Value = true;
                            break;

                        case StableRankingMode.Friends:
                            selectedModsToggle.Active.Value = false;
                            scopeDropdown.Current.Value = BeatmapLeaderboardScope.Friend;
                            break;

                        case StableRankingMode.Country:
                            selectedModsToggle.Active.Value = false;
                            scopeDropdown.Current.Value = BeatmapLeaderboardScope.Country;
                            break;
                    }
                    syncingStableRanking = false;
                });

                interfaceTheme.BindValueChanged(t =>
                {
                    bool isStable = t.NewValue.IsThemed();
                    lazerHeaderContainer.Alpha = isStable ? 0f : 1f;
                    stableHeaderContainer.Alpha = isStable ? 1f : 0f;
                    if (isStable)
                        tabControl.Current.Value = Selection.Ranking;
                }, true);
            }

            private void syncToStableRankingDropdown()
            {
                if (syncingStableRanking)
                    return;

                syncingStableRanking = true;
                if (selectedModsToggle.Active.Value && scopeDropdown.Current.Value != BeatmapLeaderboardScope.Local)
                    stableRankingDropdown.Current.Value = StableRankingMode.SelectedMods;
                else
                {
                    stableRankingDropdown.Current.Value = scopeDropdown.Current.Value switch
                    {
                        BeatmapLeaderboardScope.Local => StableRankingMode.Local,
                        BeatmapLeaderboardScope.Friend => StableRankingMode.Friends,
                        BeatmapLeaderboardScope.Country => StableRankingMode.Country,
                        _ => StableRankingMode.Global,
                    };
                }
                syncingStableRanking = false;
            }

            // Relax/Autopilot boards are by pp only: stable's and lazer's scores there come from two scoring systems.
            // TumThai: a custom ruleset's boards (Normal, Relax, Autopilot) are by score only: it has no pp.
            private bool customRuleset => ruleset.Value is RulesetInfo r && !r.IsLegacyRuleset() && !r.IsSpecialRuleset();

            private LeaderboardSortMode[] onlineSortModes(LeaderboardVariant variant)
                => customRuleset
                    ? new[] { LeaderboardSortMode.Score }
                    : variant == LeaderboardVariant.Vanilla
                    ? new[] { LeaderboardSortMode.PerformancePoints, LeaderboardSortMode.Score }
                    : new[] { LeaderboardSortMode.PerformancePoints };

            private LeaderboardSortMode defaultOnlineSort(LeaderboardVariant variant)
                => customRuleset || variant == LeaderboardVariant.Vanilla ? LeaderboardSortMode.Score : LeaderboardSortMode.PerformancePoints;

            private void updateVariantOptions(RulesetInfo? rulesetInfo)
            {
                var supported = rulesetInfo.SupportedVariants();

                variantDropdown.Items = supported;
                stableVariantDropdown.Items = supported;

                if (!supported.Contains(variantDropdown.Current.Value))
                    variantDropdown.Current.Value = LeaderboardVariant.Vanilla;

                // Hide the selector for rulesets without RX/AP variants (osu!mania).
                variantDropdown.FadeTo(supported.Length > 1 ? 1 : 0, 200, Easing.OutQuint);
                stableVariantDropdown.FadeTo(supported.Length > 1 ? 1 : 0, 200, Easing.OutQuint);
            }

            protected override bool OnMouseDown(MouseDownEvent e) => true;

            #region Reading / writing state from / to configuration

            private void updateConfigDetailTab()
            {
                switch (tabControl.Current.Value)
                {
                    case Selection.Details:
                        configDetailTab.Value = BeatmapDetailTab.Details;
                        return;

                    case Selection.Ranking:
                        configDetailTab.Value = mapLeaderboardScopeToDetailTab(scopeDropdown.Current.Value);
                        return;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(tabControl.Current.Value), tabControl.Current.Value, null);
                }
            }

            private static BeatmapLeaderboardScope? tryMapDetailTabToLeaderboardScope(BeatmapDetailTab tab)
            {
                switch (tab)
                {
                    case BeatmapDetailTab.Local:
                        return BeatmapLeaderboardScope.Local;

                    case BeatmapDetailTab.Country:
                        return BeatmapLeaderboardScope.Country;

                    case BeatmapDetailTab.Global:
                        return BeatmapLeaderboardScope.Global;

                    case BeatmapDetailTab.Friends:
                        return BeatmapLeaderboardScope.Friend;

                    case BeatmapDetailTab.Team:
                        return BeatmapLeaderboardScope.Team;

                    default:
                        return null;
                }
            }

            private static BeatmapDetailTab mapLeaderboardScopeToDetailTab(BeatmapLeaderboardScope scope)
            {
                switch (scope)
                {
                    case BeatmapLeaderboardScope.Local:
                        return BeatmapDetailTab.Local;

                    case BeatmapLeaderboardScope.Country:
                        return BeatmapDetailTab.Country;

                    case BeatmapLeaderboardScope.Global:
                        return BeatmapDetailTab.Global;

                    case BeatmapLeaderboardScope.Friend:
                        return BeatmapDetailTab.Friends;

                    case BeatmapLeaderboardScope.Team:
                        return BeatmapDetailTab.Team;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
                }
            }

            #endregion

            public enum Selection
            {
                [LocalisableDescription(typeof(SongSelectStrings), nameof(SongSelectStrings.Details))]
                Details,

                [LocalisableDescription(typeof(SongSelectStrings), nameof(SongSelectStrings.Ranking))]
                Ranking,
            }

            private partial class ScopeDropdown : ShearedDropdown<BeatmapLeaderboardScope>
            {
                public ScopeDropdown()
                    : base(BeatmapLeaderboardWedgeStrings.Scope)
                {
                    Items = Enum.GetValues<BeatmapLeaderboardScope>();
                }

                protected override LocalisableString GenerateItemText(BeatmapLeaderboardScope item) => item.GetLocalisableDescription();
            }

            private partial class VariantDropdown : ShearedDropdown<LeaderboardVariant>
            {
                public VariantDropdown()
                    : base(@"Variant")
                {
                    Items = new[] { LeaderboardVariant.Vanilla };
                }

                protected override LocalisableString GenerateItemText(LeaderboardVariant item)
                {
                    switch (item)
                    {
                        case LeaderboardVariant.Relax:
                            return @"Relax";

                        case LeaderboardVariant.Autopilot:
                            return @"Autopilot";

                        default:
                            return @"Vanilla";
                    }
                }
            }

            public enum StableRankingMode
            {
                Local,
                Global,
                SelectedMods,
                Friends,
                Country,
            }

            private partial class StableRankingModeDropdown : StableCyanDropdown<StableRankingMode>
            {
                public StableRankingModeDropdown()
                {
                    Items = Enum.GetValues<StableRankingMode>();
                }

                protected override LocalisableString GenerateItemText(StableRankingMode item) => item switch
                {
                    StableRankingMode.Local => @"Local Ranking",
                    StableRankingMode.Global => @"Global Ranking",
                    StableRankingMode.SelectedMods => @"Selected Mods",
                    StableRankingMode.Friends => @"Friends Ranking",
                    StableRankingMode.Country => @"Country Ranking",
                    _ => item.ToString(),
                };
            }

            private partial class StableCyanDropdown<T> : OsuDropdown<T>
                where T : struct, Enum
            {
                protected override DropdownHeader CreateHeader() => new StableCyanDropdownHeader();

                protected override LocalisableString GenerateItemText(T item) => item.GetLocalisableDescription();

                private partial class StableCyanDropdownHeader : OsuDropdownHeader
                {
                    public StableCyanDropdownHeader()
                    {
                        CornerRadius = 2f;
                        Height = 25f;
                        Masking = true;
                        BorderThickness = 1.5f;
                        BorderColour = Color4Extensions.FromHex(@"1FB8FF");
                        Foreground.Padding = new MarginPadding { Top = 3, Bottom = 3, Left = 8, Right = 8 };
                        Text.Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold);
                    }

                    [BackgroundDependencyLoader]
                    private void load()
                    {
                        BackgroundColour = Color4.Black.Opacity(0.92f);
                        BackgroundColourHover = Color4Extensions.FromHex(@"0D293E");
                    }
                }
            }
        }
    }
}
