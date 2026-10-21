// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterface
{
    /// <summary>
    /// Compact real-time latency and frame-time breakdown counter showing Audio, Input, and Draw latency in milliseconds.
    /// </summary>
    public partial class LatencyPerformanceCounter : VisibilityContainer
    {
        private OsuSpriteText text = null!;

        private Bindable<bool> showLatencyCounter = null!;
        private Bindable<ExecutionMode> executionMode = null!;
        private IBindable<double> audioLatency = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private double lastUpdate;
        private double smoothedAudioMs;
        private double smoothedInputMs;
        private double smoothedDrawMs;
        private double smoothedFps;

        private const double update_interval_ms = 100;
        private const double smoothing_factor = 0.25;

        public LatencyPerformanceCounter()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, FrameworkConfigManager frameworkConfig, AudioManager audio)
        {
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 5,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black,
                            Alpha = 0.65f,
                        },
                        text = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold, fixedWidth: false),
                            Colour = new Color4(235, 240, 245, 255),
                            Margin = new MarginPadding { Horizontal = 8, Vertical = 4 },
                        },
                    },
                },
            };

            executionMode = frameworkConfig.GetBindable<ExecutionMode>(FrameworkSetting.ExecutionMode);
            audioLatency = audio.OutputLatency.GetBoundCopy();

            showLatencyCounter = config.GetBindable<bool>(OsuSetting.ShowLatencyCounter);
            showLatencyCounter.BindValueChanged(v => State.Value = v.NewValue ? Visibility.Visible : Visibility.Hidden, true);
        }

        protected override void PopIn() => this.FadeIn(150, Easing.OutQuint);

        protected override void PopOut() => this.FadeOut(150, Easing.OutQuint);

        protected override bool OnClick(ClickEvent e)
        {
            showLatencyCounter.Value = false;
            return true;
        }

        protected override void Update()
        {
            base.Update();

            if (State.Value == Visibility.Hidden)
                return;

            if (Time.Current - lastUpdate < update_interval_ms)
                return;

            lastUpdate = Time.Current;

            double rawAudioMs = Math.Max(0, audioLatency.Value);
            double rawInputMs = getThreadElapsedMs(executionMode.Value == ExecutionMode.SingleThread ? host.UpdateThread : host.InputThread);
            double rawDrawMs = getThreadElapsedMs(host.DrawThread);
            double rawFps = host.DrawThread?.Clock.FramesPerSecond ?? 0;

            smoothedAudioMs = smooth(smoothedAudioMs, rawAudioMs);
            smoothedInputMs = smooth(smoothedInputMs, rawInputMs);
            smoothedDrawMs = smooth(smoothedDrawMs, rawDrawMs);
            smoothedFps = smooth(smoothedFps, rawFps);

            string audioPart = smoothedAudioMs > 0.05 ? $"{smoothedAudioMs:0.0}ms" : "--";
            text.Text = $"Audio {audioPart}  ·  Input {smoothedInputMs:0.00}ms  ·  Draw {smoothedDrawMs:0.00}ms ({Math.Round(smoothedFps):0} fps)";
        }

        private static double getThreadElapsedMs(GameThread? thread)
        {
            if (thread == null)
                return 0;

            double fps = thread.Clock.FramesPerSecond;
            if (fps > 0)
                return Math.Clamp(1000.0 / fps, 0, 999.9);

            return Math.Clamp(thread.Clock.ElapsedFrameTime, 0, 999.9);
        }

        private static double smooth(double current, double target)
            => current <= 0 ? target : current + (target - current) * smoothing_factor;
    }
}
