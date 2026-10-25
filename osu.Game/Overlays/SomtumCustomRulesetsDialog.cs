// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    /// <summary>
    /// TumThai: the custom rulesets the server ranks, to install, update or remove (Settings > General > Custom rulesets).
    /// One row each: its icon, name, the version the server ranks and where it stands here. A new ruleset is loaded at once;
    /// updating or removing one that's loaded takes a restart (Windows keeps its DLL locked).
    /// </summary>
    public partial class SomtumCustomRulesetsDialog : OsuFocusedOverlayContainer
    {
        private const double enter_duration = 500;
        private const double exit_duration = 200;

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        [Resolved]
        private SomtumCustomRulesets customRulesets { get; set; } = null!;

        private FillFlowContainer<Row> list = null!;
        private OsuTextFlowContainer note = null!;

        public SomtumCustomRulesetsDialog()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(0.5f, 0.8f);

            Masking = true;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    Colour = colours.GreySeaFoamDark,
                    RelativeSizeAxes = Axes.Both,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Text = @"Custom rulesets",
                                        Font = OsuFont.GetFont(size: 30),
                                        Padding = new MarginPadding { Vertical = 10 },
                                    },
                                    new IconButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Icon = FontAwesome.Solid.Times,
                                        Colour = colours.GreySeaFoamDarker,
                                        Scale = new Vector2(0.8f),
                                        X = -10,
                                        Action = Hide,
                                    },
                                },
                            },
                        },
                        new Drawable[]
                        {
                            note = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                TextAnchor = Anchor.TopCentre,
                                Padding = new MarginPadding { Horizontal = 20, Bottom = 10 },
                                Colour = colours.GreySeaFoamLighter,
                            },
                        },
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = colours.GreySeaFoamDarker,
                                    },
                                    new OsuScrollContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Child = list = new FillFlowContainer<Row>
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 8),
                                            Padding = new MarginPadding(15),
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            customRulesets.Available.BindCollectionChanged((_, _) => rebuild(), true);
        }

        private void rebuild()
        {
            list.Clear();
            note.Text = customRulesets.Available.Count == 0
                ? "The custom rulesets this server ranks show here once you're signed in."
                : "Rulesets from rulesets.info that this server ranks, on their own leaderboards (by score). A new one is ready to play "
                  + "right after it installs; updating or removing one you've already loaded takes a restart.";

            foreach (var entry in customRulesets.Available.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
                list.Add(new Row(entry));
        }

        protected override void PopIn()
        {
            customRulesets.Refresh();
            this.FadeIn(enter_duration, Easing.OutQuint);
            this.ScaleTo(0.9f).Then().ScaleTo(1f, enter_duration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            base.PopOut();
            this.FadeOut(exit_duration, Easing.OutQuint);
            this.ScaleTo(0.9f, exit_duration);
        }

        private partial class Row : CompositeDrawable
        {
            private readonly APISomtumCustomRuleset entry;

            [Resolved]
            private SomtumCustomRulesets customRulesets { get; set; } = null!;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            private OsuSpriteText state = null!;
            private RoundedButton button = null!;
            private bool busy;

            public Row(APISomtumCustomRuleset entry)
            {
                this.entry = entry;
                RelativeSizeAxes = Axes.X;
                Height = 76;
                Masking = true;
                CornerRadius = 8;
            }

            [BackgroundDependencyLoader]
            private void load(LargeTextureStore textures)
            {
                Texture? icon = string.IsNullOrEmpty(entry.Icon) ? null : textures.Get(entry.Icon);

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.GreySeaFoamDark,
                    },
                    new Container
                    {
                        Size = new Vector2(56),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 10,
                        Child = icon != null
                            ? new Sprite
                            {
                                RelativeSizeAxes = Axes.Both,
                                FillMode = FillMode.Fit,
                                Texture = icon,
                            }
                            : new SpriteIcon
                            {
                                RelativeSizeAxes = Axes.Both,
                                Icon = FontAwesome.Solid.PuzzlePiece,
                                Colour = colours.GreySeaFoamLighter,
                            },
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Left = 80, Right = 170 },
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = entry.Name,
                                Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                            },
                            state = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                            },
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = entry.Description ?? string.Empty,
                                Font = OsuFont.GetFont(size: 13),
                                Colour = colours.GreySeaFoamLighter,
                            },
                        },
                    },
                    button = new RoundedButton
                    {
                        Width = 140,
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -15,
                        Action = act,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                refresh();
            }

            private void refresh(string? message = null, bool failed = false)
            {
                string version = string.IsNullOrEmpty(entry.Version) ? string.Empty : $"{entry.Version} · ";
                var current = customRulesets.StateOf(entry);

                (string text, Color4 colour, string? action) = current switch
                {
                    CustomRulesetState.NotInstalled => ("Not installed", colours.GreySeaFoamLighter, "Install"),
                    CustomRulesetState.UpdateAvailable => ("An update is ready (the server ranks this version)", colours.Yellow, "Update"),
                    CustomRulesetState.Installed => ("Installed", colours.Green, "Remove"),
                    CustomRulesetState.RestartToLoad => ("Installed: restart TumThai to load it", colours.Yellow, null),
                    CustomRulesetState.RestartToUpdate => ("Updated: restart TumThai to use it", colours.Yellow, null),
                    CustomRulesetState.RestartToRemove => ("Removed when TumThai restarts", colours.Yellow, null),
                    CustomRulesetState.Broken => ("Doesn't work with this TumThai version", colours.Red, "Remove"),
                    _ => (string.Empty, colours.GreySeaFoamLighter, (string?)null),
                };

                state.Text = version + (message ?? text);
                state.Colour = failed ? colours.Red : colour;

                button.Text = action ?? "Restart needed";
                button.Enabled.Value = action != null && !busy;
                button.BackgroundColour = action == "Remove" ? colours.Red3 : colours.Blue3;
            }

            private void act()
            {
                var current = customRulesets.StateOf(entry);

                if (current is CustomRulesetState.Installed or CustomRulesetState.Broken)
                {
                    try
                    {
                        customRulesets.Remove(entry);
                        refresh();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $@"Couldn't remove the {entry.Name} ruleset");
                        refresh($"Couldn't remove it: {e.Message}", true);
                    }

                    return;
                }

                busy = true;
                button.Enabled.Value = false;
                button.Text = "Downloading…";

                Task.Run(() => customRulesets.InstallAsync(entry)).ContinueWith(t => Schedule(() =>
                {
                    busy = false;

                    if (t.IsFaulted)
                    {
                        Logger.Error(t.Exception, $@"Couldn't install the {entry.Name} ruleset");
                        refresh($"Couldn't install it: {t.Exception?.GetBaseException().Message}", true);
                        return;
                    }

                    refresh(t.GetResultSafely() ? "Installed: it's in the ruleset list now" : null);
                }));
            }
        }
    }
}
