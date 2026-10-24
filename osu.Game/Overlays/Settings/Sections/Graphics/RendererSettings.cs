// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.Graphics
{
    public partial class RendererSettings : SettingsSubsection
    {
        protected override LocalisableString Header => GraphicsSettingsStrings.RendererHeader;

        private bool automaticRendererInUse;

        private FormCheckBox? dangerousUnlimitedCheckbox;
        private readonly Bindable<SettingsNote.Data?> dangerousUnlimitedNote = new Bindable<SettingsNote.Data?>();
        private readonly Bindable<SettingsNote.Data?> inputAudioHzNote = new Bindable<SettingsNote.Data?>();
        private readonly Bindable<SettingsNote.Data?> frameSyncNote = new Bindable<SettingsNote.Data?>();

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager config, OsuConfigManager osuConfig, IDialogOverlay? dialogOverlay, OsuGame? game, GameHost host)
        {
            var renderer = config.GetBindable<RendererType>(FrameworkSetting.Renderer);
            automaticRendererInUse = renderer.Value == RendererType.Automatic;

            var dangerousUnlimitedNoCap = config.GetBindable<bool>(FrameworkSetting.AllowDangerousUnlimitedNoCap);
            var frameSync = config.GetBindable<FrameSync>(FrameworkSetting.FrameSync);
            var inputAudioHz = osuConfig.GetBindable<InputAudioHzMode>(OsuSetting.InputAudioHz);
            var displayRefreshRate = osuConfig.GetBindable<DisplayRefreshRateMode>(OsuSetting.DisplayRefreshRate);

            IEnumerable<RendererType> availableRenderers = host.GetPreferredRenderersForCurrentPlatform().Order();

            // Keep Deferred_Vulkan hidden unless already active; standard Veldrid Vulkan is exposed on Windows and Linux.
            if (renderer.Value != RendererType.Deferred_Vulkan)
                availableRenderers = availableRenderers.Where(t => t != RendererType.Deferred_Vulkan);

            var children = new List<Drawable>
            {
                new SettingsItemV2(new RendererDropdown
                {
                    Caption = GraphicsSettingsStrings.Renderer,
                    Current = renderer,
                    Items = availableRenderers,
                })
                {
                    Keywords = new[] { @"compatibility", @"directx", @"vulkan", @"opengl", @"metal" },
                },
                // TODO: this needs to be a custom dropdown at some point
                new SettingsItemV2(new FormEnumDropdown<FrameSync>
                {
                    Caption = GraphicsSettingsStrings.FrameLimiter,
                    Current = frameSync,
                })
                {
                    Keywords = new[] { @"fps", @"framerate" },
                    Note = { BindTarget = frameSyncNote },
                },
                new SettingsItemV2(new FormEnumDropdown<DisplayRefreshRateMode>
                {
                    Caption = "Display refresh rate",
                    HintText = "Selects the target screen refresh rate (60 / 90 / 120 / 144 / 165 / 240 Hz) on Android and for 2x / 4x / 8x frame limiter multipliers.",
                    Current = displayRefreshRate,
                })
                {
                    Keywords = new[] { @"hz", @"refresh", @"display", @"60", @"90", @"120", @"144", @"165", @"240", @"fps" },
                },
                new SettingsItemV2(new FormEnumDropdown<InputAudioHzMode>
                {
                    Caption = "Input / audio rate",
                    HintText = "Controls how frequently input, audio, and multi-threaded gameplay updates run.",
                    Current = inputAudioHz,
                })
                {
                    Keywords = new[] { @"hz", @"polling", @"input", @"audio", @"update", @"latency", @"1000", @"2000", @"4000", @"8000" },
                    Note = { BindTarget = inputAudioHzNote },
                },
                new SettingsItemV2(dangerousUnlimitedCheckbox = new FormCheckBox
                {
                    Caption = "Uncap all threads in Unlimited mode (experimental)",
                    HintText = "Allows the experimental Unlimited mode to uncap update, input, and audio scheduling too. This can cause audio pops, stutters, and extra heat.",
                    Current = dangerousUnlimitedNoCap,
                })
                {
                    Keywords = new[] { @"fps", @"unlimited", @"no cap", @"danger", @"audio" },
                    Note = { BindTarget = dangerousUnlimitedNote },
                },
                new SettingsItemV2(new FormEnumDropdown<ExecutionMode>
                {
                    Caption = GraphicsSettingsStrings.ThreadingMode,
                    Current = config.GetBindable<ExecutionMode>(FrameworkSetting.ExecutionMode)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = GraphicsSettingsStrings.ShowFPS,
                    Current = osuConfig.GetBindable<bool>(OsuSetting.ShowFpsDisplay),
                })
                {
                    Keywords = new[] { @"framerate", @"counter" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Show latency counter (Audio / Input / Draw ms)",
                    HintText = "Displays a compact real-time breakdown of audio output latency, input interval, and draw frame time.",
                    Current = osuConfig.GetBindable<bool>(OsuSetting.ShowLatencyCounter),
                })
                {
                    Keywords = new[] { @"latency", @"ms", @"fps", @"counter", @"performance", @"audio" },
                },
            };

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Android)
            {
                children.Add(new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Sustained performance mode",
                    HintText = "Hints Android to maintain steady CPU/GPU clocks and reduce thermal throttling during long sessions.",
                    Current = osuConfig.GetBindable<bool>(OsuSetting.AndroidSustainedPerformance),
                })
                {
                    Keywords = new[] { @"android", @"thermal", @"sustained", @"performance", @"throttling" },
                });
            }

            children.Add(new PotatoModeToggleAndRestart());

            Children = children;

            inputAudioHz.BindValueChanged(v =>
            {
                if (Performance.PotatoMode.Active)
                {
                    inputAudioHzNote.Value = new SettingsNote.Data(
                        "Potato mode is active and forces the input/audio/update rate to 500 Hz to save CPU.",
                        SettingsNote.Type.Informational);
                    return;
                }

                inputAudioHzNote.Value = v.NewValue switch
                {
                    InputAudioHzMode.Hz500 => new SettingsNote.Data(
                        "500 Hz reduces CPU usage and battery drain on weaker laptops and mobile devices.",
                        SettingsNote.Type.Informational),
                    InputAudioHzMode.Hz1000 => new SettingsNote.Data(
                        "1000 Hz is the standard rate and works well for most 125-1000 Hz mice, tablets, and keyboards.",
                        SettingsNote.Type.Informational),
                    InputAudioHzMode.Hz2000 => new SettingsNote.Data(
                        "2000 Hz gives lower input/update quantization for competitive play and high-polling peripherals.",
                        SettingsNote.Type.Informational),
                    InputAudioHzMode.Hz4000 => new SettingsNote.Data(
                        "4000 Hz is intended for 2000-4000 Hz mice/keyboards on fast multi-core desktop CPUs.",
                        SettingsNote.Type.Warning),
                    InputAudioHzMode.Hz8000 => new SettingsNote.Data(
                        "8000 Hz uses significant CPU time. Lower this to 2000 or 4000 Hz if you notice frame drops or audio crackles.",
                        SettingsNote.Type.Warning),
                    _ => null
                };
            }, true);

            // TumThai!Next: Unlimited renders as fast as the GPU goes (1000+ fps here, unlike TumThai), so say what it costs.
            frameSync.BindValueChanged(v =>
            {
                frameSyncNote.Value = v.NewValue == FrameSync.Unlimited
                    ? new SettingsNote.Data(
                        "Unlimited renders as fast as your graphics card can (often 1000+ fps): more heat, power use and fan noise, and on some systems stutters or crashes. "
                        + "It's experimental in TumThai!Next. 2x, 4x or 8x refresh rate is usually just as smooth.",
                        SettingsNote.Type.Warning)
                    : null;
            }, true);

            dangerousUnlimitedNoCap.BindValueChanged(v =>
            {
                dangerousUnlimitedNote.Value = v.NewValue
                    ? new SettingsNote.Data("Unsafe mode enabled: Unlimited can now uncap update/input/audio too. Disable this first if audio starts doubling, popping, or stuttering.", SettingsNote.Type.Warning)
                    : new SettingsNote.Data("Recommended: leave this off. Unlimited will still uncap rendering, but keeps audio/input/update protected.", SettingsNote.Type.Informational);
            }, true);

            bool isDeferredRenderer(RendererType t) =>
                t == RendererType.Deferred_Direct3D11
                || t == RendererType.Deferred_Metal
                || t == RendererType.Deferred_OpenGL
                || t == RendererType.Deferred_Vulkan;

            void applyDangerousUnlimitedGate(RendererType resolvedRenderer)
            {
                if (isDeferredRenderer(resolvedRenderer))
                {
                    if (dangerousUnlimitedNoCap.Value)
                        dangerousUnlimitedNoCap.Value = false;

                    dangerousUnlimitedNoCap.Disabled = true;
                    if (dangerousUnlimitedCheckbox != null)
                        dangerousUnlimitedCheckbox.Current.Disabled = true;

                    dangerousUnlimitedNote.Value = new SettingsNote.Data(
                        "Disabled on the Deferred renderer: uncapped update + Deferred = unbounded memory growth + crash. Switch to a non-deferred renderer if you really want this.",
                        SettingsNote.Type.Warning);
                }
                else
                {
                    dangerousUnlimitedNoCap.Disabled = false;
                    if (dangerousUnlimitedCheckbox != null)
                        dangerousUnlimitedCheckbox.Current.Disabled = false;
                }
            }

            applyDangerousUnlimitedGate(host.ResolvedRenderer);

            renderer.BindValueChanged(r =>
            {
                if (r.NewValue == host.ResolvedRenderer)
                    return;

                // Need to check startup renderer for the "automatic" case, as ResolvedRenderer above will track the final resolved renderer instead.
                if (r.NewValue == RendererType.Automatic && automaticRendererInUse)
                    return;

                if (game?.RestartAppWhenExited() == true)
                {
                    game.AttemptExit();
                }
                else
                {
                    dialogOverlay?.Push(new ConfirmDialog(GraphicsSettingsStrings.ChangeRendererConfirmation, () => game?.AttemptExit(), () =>
                    {
                        renderer.Value = automaticRendererInUse ? RendererType.Automatic : host.ResolvedRenderer;
                    }));
                }
            });
        }

        private partial class RendererDropdown : FormEnumDropdown<RendererType>
        {
            private RendererType hostResolvedRenderer;
            private bool automaticRendererInUse;

            [BackgroundDependencyLoader]
            private void load(FrameworkConfigManager config, GameHost host)
            {
                var renderer = config.GetBindable<RendererType>(FrameworkSetting.Renderer);
                automaticRendererInUse = renderer.Value == RendererType.Automatic;
                hostResolvedRenderer = host.ResolvedRenderer;
            }

            protected override LocalisableString GenerateItemText(RendererType item)
            {
                if (item == RendererType.Automatic && automaticRendererInUse)
                    return LocalisableString.Interpolate($"{base.GenerateItemText(item)} ({hostResolvedRenderer.GetDescription()})");

                return base.GenerateItemText(item);
            }
        }
    }
}
