// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Performance;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Overlays.Mods
{
    /// <summary>
    /// osu!stable 3-row Mod Selection screen layout ("Difficulty Reduction", "Difficulty Increase", "Special")
    /// with score multiplier display, multi-state mod cycling (DT/NC, SD/PF, HT/DC, AT/CN),
    /// keyboard shortcuts (Q/W/E, A/S/D/F/G, Z/X/C/V/B, 1, 2), and 3-slice bottom buttons.
    /// </summary>
    public partial class StableModSelectContent : CompositeDrawable
    {
        private readonly ModSelectOverlay overlay;
        private readonly Action toggleAllLazerModsView;

        private OsuSpriteText multiplierText = null!;
        private FillFlowContainer reductionRowFlow = null!;
        private FillFlowContainer increaseRowFlow = null!;
        private FillFlowContainer specialRowFlow = null!;

        private readonly List<StableModSlotButton> slotButtons = new List<StableModSlotButton>();

        private ISample? checkOnSample;
        private ISample? checkOffSample;
        private ISample? backSample;

        private static readonly Color4 colour_reduction = Color4Extensions.FromHex(@"32CD32");
        private static readonly Color4 colour_increase = Color4Extensions.FromHex(@"FF4500");
        private static readonly Color4 colour_multiplier_high = Color4Extensions.FromHex(@"ADFF2F");
        private static readonly Color4 colour_multiplier_low = Color4Extensions.FromHex(@"FF4500");

        public StableModSelectContent(ModSelectOverlay overlay, Action toggleAllLazerModsView)
        {
            this.overlay = overlay;
            this.toggleAllLazerModsView = toggleAllLazerModsView;
            RelativeSizeAxes = Axes.Both;
        }

        [Resolved(canBeNull: true)]
        private IBindable<WorkingBeatmap>? beatmap { get; set; }

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        private IBindable<Skin> currentSkin = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            updateSamples();

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.9f),
                },
                // Top-left header text matching osu!stable
                new FillFlowContainer
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Left = 24, Top = 18 },
                    Spacing = new Vector2(0, 4),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Mod Selection",
                            Font = OsuFont.GetFont(size: 32, weight: FontWeight.Bold),
                            Colour = Color4.White,
                            Shadow = true,
                        },
                        new OsuSpriteText
                        {
                            Text = "Mods provide different ways to enjoy gameplay. Some have an effect on the score you can achieve during ranked play!",
                            Font = OsuFont.GetFont(size: 15, weight: FontWeight.Regular),
                            Colour = Color4.White.Opacity(0.9f),
                            Shadow = true,
                        },
                        new OsuSpriteText
                        {
                            Text = "Others are just for fun :)",
                            Font = OsuFont.GetFont(size: 15, weight: FontWeight.Regular),
                            Colour = Color4.White.Opacity(0.9f),
                            Shadow = true,
                        },
                    },
                },
                // Score Multiplier (top-center above rows)
                multiplierText = new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 108,
                    Font = OsuFont.GetFont(size: 30, weight: FontWeight.Bold),
                    Colour = Color4.White,
                    Shadow = true,
                },
                // 3-Row Mod Grid
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Y = -10,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 18),
                    Children = new Drawable[]
                    {
                        createModRow("Difficulty Reduction", colour_reduction, out reductionRowFlow),
                        createModRow("Difficulty Increase", colour_increase, out increaseRowFlow),
                        createModRow("Special", Color4.White, out specialRowFlow),
                    },
                },
                // Bottom 3-slice buttons
                new FillFlowContainer
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -28,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(16, 0),
                            Children = new Drawable[]
                            {
                                new StableThreeSliceButton
                                {
                                    Size = new Vector2(310, 42),
                                    Text = "1. Reset All Mods",
                                    BackgroundColour = Color4Extensions.FromHex(@"FF4500"),
                                    Action = () =>
                                    {
                                        checkOffSample?.Play();
                                        overlay.DeselectAll();
                                    },
                                },
                                new StableThreeSliceButton
                                {
                                    Size = new Vector2(310, 42),
                                    Text = "2. Close",
                                    BackgroundColour = Color4Extensions.FromHex(@"808080"),
                                    Action = () =>
                                    {
                                        backSample?.Play();
                                        overlay.Hide();
                                    },
                                },
                            },
                        },
                        new StableThreeSliceButton
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Size = new Vector2(340, 34),
                            Text = "3. Customise / All Lazer Mods...",
                            FontSize = 15,
                            BackgroundColour = Color4Extensions.FromHex(@"1C6BA0"),
                            Action = toggleAllLazerModsView,
                        },
                    },
                },
            };
        }

        private Drawable createModRow(string label, Color4 labelColour, out FillFlowContainer rowIconsFlow)
        {
            return new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(20, 0),
                Children = new Drawable[]
                {
                    new Container
                    {
                        Width = 230,
                        Height = 76,
                        Child = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = label,
                            Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold),
                            Colour = labelColour,
                            Shadow = true,
                        },
                    },
                    rowIconsFlow = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(18, 0),
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentSkin = skinManager.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ =>
            {
                updateSamples();
                updateVisualState();
            });

            overlay.AvailableMods.BindValueChanged(_ => rebuildSlots(), true);
            overlay.SelectedMods.BindValueChanged(_ => updateVisualState(), true);
        }

        private void updateSamples()
        {
            checkOnSample = skinManager.GetClassicSample(@"check-on", @"click-short-confirm") ?? audio.Samples.Get(@"UI/check-on");
            checkOffSample = skinManager.GetClassicSample(@"check-off", @"click-short") ?? audio.Samples.Get(@"UI/check-off");
            backSample = skinManager.GetClassicSample(@"menuback") ?? audio.Samples.Get(@"UI/generic-select");
        }

        private record SlotSpec(string[] Acronyms, Key? ShortcutKey, string? KeyLabel);

        private static readonly SlotSpec[] reduction_specs =
        {
            new SlotSpec(new[] { @"EZ" }, Key.Q, @"Q"),
            new SlotSpec(new[] { @"NF" }, Key.W, @"W"),
            new SlotSpec(new[] { @"HT", @"DC" }, Key.E, @"E"),
            new SlotSpec(new[] { @"NR" }, null, null),
        };

        private static readonly SlotSpec[] increase_specs =
        {
            new SlotSpec(new[] { @"HR" }, Key.A, @"A"),
            new SlotSpec(new[] { @"SD", @"PF" }, Key.S, @"S"),
            new SlotSpec(new[] { @"DT", @"NC" }, Key.D, @"D"),
            new SlotSpec(new[] { @"HD" }, Key.F, @"F"),
            new SlotSpec(new[] { @"FL", @"BL" }, Key.G, @"G"),
            new SlotSpec(new[] { @"FI" }, null, null),
            new SlotSpec(new[] { @"AC" }, null, null),
        };

        private static readonly SlotSpec[] special_specs =
        {
            new SlotSpec(new[] { @"RX" }, Key.Z, @"Z"),
            new SlotSpec(new[] { @"AP" }, Key.X, @"X"),
            new SlotSpec(new[] { @"SO" }, Key.C, @"C"),
            new SlotSpec(new[] { @"AT", @"CN" }, Key.V, @"V"),
            new SlotSpec(new[] { @"CL" }, Key.B, @"B"),
            new SlotSpec(new[] { @"TP" }, null, null),
            new SlotSpec(new[] { @"DA" }, null, null),
            new SlotSpec(new[] { @"RD" }, null, null),
            new SlotSpec(new[] { @"MR" }, null, null),
            new SlotSpec(new[] { @"4K", @"5K", @"6K", @"7K", @"8K", @"9K" }, null, @"Keys"),
        };

        private void rebuildSlots()
        {
            slotButtons.Clear();
            reductionRowFlow.Clear();
            increaseRowFlow.Clear();
            specialRowFlow.Clear();

            var allStates = overlay.AllAvailableMods.Where(m => m.ValidForSelection.Value).ToList();

            populateRow(reductionRowFlow, reduction_specs, allStates);
            populateRow(increaseRowFlow, increase_specs, allStates);
            populateRow(specialRowFlow, special_specs, allStates);

            updateVisualState();
        }

        private void populateRow(FillFlowContainer rowFlow, IEnumerable<SlotSpec> specs, List<ModState> allStates)
        {
            foreach (var spec in specs)
            {
                var matchingStates = spec.Acronyms
                                         .Select(acronym => allStates.FirstOrDefault(s => s.Mod.Acronym.Equals(acronym, StringComparison.OrdinalIgnoreCase)))
                                         .Where(s => s != null)
                                         .Cast<ModState>()
                                         .ToList();

                if (matchingStates.Count == 0)
                    continue;

                var btn = new StableModSlotButton(matchingStates, spec.ShortcutKey, spec.KeyLabel, onSlotCycled);
                slotButtons.Add(btn);
                rowFlow.Add(btn);
            }
        }

        private void onSlotCycled(bool activated)
        {
            if (activated)
                checkOnSample?.Play();
            else
                checkOffSample?.Play();
        }

        private void updateVisualState()
        {
            foreach (var btn in slotButtons)
                btn.UpdateFromModState();

            var difficulty = beatmap?.Value?.BeatmapInfo.Difficulty ?? new BeatmapDifficulty();
            double multiplier = overlay.Ruleset.Value?.CreateInstance().CreateScoreMultiplierCalculator(new ScoreMultiplierContext(difficulty)).CalculateFor(overlay.SelectedMods.Value) ?? 1.0;

            multiplierText.Text = $"Score Multiplier: {multiplier:0.00}x";

            if (Precision.AlmostEquals(multiplier, 1.0, 0.005))
                multiplierText.Colour = Color4.White;
            else if (multiplier > 1.0)
                multiplierText.Colour = colour_multiplier_high;
            else
                multiplierText.Colour = colour_multiplier_low;
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (Alpha == 0 || !IsPresent || e.Repeat || e.ControlPressed || e.AltPressed)
                return base.OnKeyDown(e);

            if (e.Key == Key.Number1 || e.Key == Key.Keypad1)
            {
                checkOffSample?.Play();
                overlay.DeselectAll();
                return true;
            }

            if (e.Key == Key.Number2 || e.Key == Key.Keypad2)
            {
                backSample?.Play();
                overlay.Hide();
                return true;
            }

            if (e.Key == Key.Number3 || e.Key == Key.Keypad3)
            {
                toggleAllLazerModsView();
                return true;
            }

            foreach (var btn in slotButtons)
            {
                if (btn.ShortcutKey == e.Key)
                {
                    btn.CycleState(reverse: e.ShiftPressed);
                    return true;
                }
            }

            return base.OnKeyDown(e);
        }

        public static string? GetStableModTextureName(string acronym) => acronym.ToUpperInvariant() switch
        {
            @"EZ" => @"selection-mod-easy",
            @"NF" => @"selection-mod-nofail",
            @"HT" or @"DC" => @"selection-mod-halftime",
            @"HR" => @"selection-mod-hardrock",
            @"SD" => @"selection-mod-suddendeath",
            @"PF" => @"selection-mod-perfect",
            @"DT" => @"selection-mod-doubletime",
            @"NC" => @"selection-mod-nightcore",
            @"HD" => @"selection-mod-hidden",
            @"FI" => @"selection-mod-fadein",
            @"FL" or @"BL" => @"selection-mod-flashlight",
            @"RX" => @"selection-mod-relax",
            @"AP" => @"selection-mod-relax2",
            @"SO" => @"selection-mod-spunout",
            @"AT" => @"selection-mod-autoplay",
            @"CN" => @"selection-mod-cinema",
            @"TP" => @"selection-mod-target",
            @"SV2" => @"selection-mod-scorev2",
            @"RD" => @"selection-mod-random",
            @"MR" => @"selection-mod-mirror",
            @"1K" => @"selection-mod-key1",
            @"2K" => @"selection-mod-key2",
            @"3K" => @"selection-mod-key3",
            @"4K" => @"selection-mod-key4",
            @"5K" => @"selection-mod-key5",
            @"6K" => @"selection-mod-key6",
            @"7K" => @"selection-mod-key7",
            @"8K" => @"selection-mod-key8",
            @"9K" => @"selection-mod-key9",
            @"2P" or @"DS" => @"selection-mod-keycoop",
            _ => null,
        };

        private partial class StableModSlotButton : OsuClickableContainer
        {
            private readonly IReadOnlyList<ModState> states;
            public readonly Key? ShortcutKey;
            private readonly string? keyLabel;
            private readonly Action<bool> onCycled;

            [Resolved]
            private SkinManager skinManager { get; set; } = null!;

            private Container iconHolder = null!;
            private Sprite stableModSprite = null!;
            private ModIcon? fallbackModIcon;
            private Sprite lightingGlow = null!;

            public StableModSlotButton(IReadOnlyList<ModState> states, Key? shortcutKey, string? keyLabel, Action<bool> onCycled)
            {
                this.states = states;
                ShortcutKey = shortcutKey;
                this.keyLabel = keyLabel;
                this.onCycled = onCycled;

                Size = new Vector2(88, 76);
                Action = () => CycleState(reverse: false);
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    lightingGlow = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(132),
                        Texture = skinManager.GetClassicTexture(@"lighting"),
                        Colour = Color4Extensions.FromHex(@"89CFF0"),
                        Blending = BlendingParameters.Additive,
                        Alpha = 0,
                    },
                    iconHolder = new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Child = stableModSprite = new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                    },
                };
            }

            public void CycleState(bool reverse)
            {
                int activeIndex = -1;
                for (int i = 0; i < states.Count; i++)
                {
                    if (states[i].Active.Value)
                    {
                        activeIndex = i;
                        break;
                    }
                }

                int nextIndex;
                if (!reverse)
                {
                    nextIndex = activeIndex + 1;
                    if (nextIndex >= states.Count)
                        nextIndex = -1;
                }
                else
                {
                    nextIndex = activeIndex - 1;
                    if (nextIndex < -1)
                        nextIndex = states.Count - 1;
                }

                for (int i = 0; i < states.Count; i++)
                {
                    if (i != nextIndex && states[i].Active.Value)
                        states[i].Active.Value = false;
                }

                if (nextIndex >= 0)
                {
                    states[nextIndex].Active.Value = true;
                    onCycled(true);
                }
                else
                {
                    onCycled(false);
                }

                UpdateFromModState();
            }

            public void UpdateFromModState()
            {
                ModState? activeState = states.FirstOrDefault(s => s.Active.Value);
                Mod displayedMod = activeState?.Mod ?? states[0].Mod;

                string? texName = GetStableModTextureName(displayedMod.Acronym);
                var tex = texName != null ? skinManager.GetClassicTexture(texName) : null;
                lightingGlow.Texture = skinManager.GetClassicTexture(@"lighting");

                if (tex != null)
                {
                    stableModSprite.Texture = tex;
                    stableModSprite.Alpha = 1f;
                    if (fallbackModIcon != null)
                        fallbackModIcon.Alpha = 0f;
                }
                else
                {
                    if (fallbackModIcon == null)
                    {
                        iconHolder.Add(fallbackModIcon = new ModIcon(displayedMod)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Scale = new Vector2(0.75f),
                        });
                    }
                    else
                    {
                        fallbackModIcon.Mod = displayedMod;
                    }

                    fallbackModIcon.Alpha = 1f;
                    stableModSprite.Alpha = 0f;
                }

                TooltipText = $"{displayedMod.Name} ({displayedMod.Acronym}): {displayedMod.Description}";

                double duration = PotatoMode.UltraActive ? 0 : 150;
                if (activeState != null)
                {
                    lightingGlow.FadeTo(0.85f, PotatoMode.UltraActive ? 0 : 180, Easing.OutQuint);
                    iconHolder.ScaleTo(1.2f, duration, Easing.OutBack);
                    iconHolder.RotateTo(5.73f, duration, Easing.OutBack);
                    iconHolder.FadeColour(Color4.White, duration);
                }
                else
                {
                    lightingGlow.FadeOut(PotatoMode.UltraActive ? 0 : 180, Easing.OutQuint);
                    iconHolder.ScaleTo(IsHovered && !PotatoMode.UltraActive ? 1.04f : 1.0f, duration, Easing.OutQuint);
                    iconHolder.RotateTo(0f, duration, Easing.OutQuint);
                    iconHolder.FadeColour(IsHovered ? Color4.White : OsuColour.Gray(0.88f), duration);
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (states.All(s => !s.Active.Value))
                {
                    if (!PotatoMode.UltraActive)
                        iconHolder.ScaleTo(1.06f, 120, Easing.OutQuint);
                    iconHolder.FadeColour(Color4.White, PotatoMode.UltraActive ? 0 : 120);
                }

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (states.All(s => !s.Active.Value))
                {
                    if (!PotatoMode.UltraActive)
                        iconHolder.ScaleTo(1.0f, 150, Easing.OutQuint);
                    iconHolder.FadeColour(OsuColour.Gray(0.88f), PotatoMode.UltraActive ? 0 : 150);
                }

                base.OnHoverLost(e);
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                if (e.Button == MouseButton.Right)
                {
                    CycleState(reverse: true);
                    return true;
                }

                return base.OnMouseDown(e);
            }
        }
    }
}
