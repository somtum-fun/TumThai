// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Screens.Play.HUD
{
    public abstract partial class PerformancePointsCounter : RollingCounter<int>
    {
        public bool UsesFixedAnchor { get; set; }

        // osu!somtum: the live pp is worked out by a LivePerformanceTracker shared with the gameplay leaderboard.
        // Player provides one; when there is none (e.g. tests), the counter makes its own.
        [Resolved(CanBeNull = true)]
        private LivePerformanceTracker sharedTracker { get; set; }

        private LivePerformanceTracker tracker;

        private readonly IBindable<double> performance = new BindableDouble();
        private readonly IBindable<bool> trackerValid = new BindableBool();

        [BackgroundDependencyLoader]
        private void load()
        {
            tracker = sharedTracker;

            if (tracker == null)
            {
                AddInternal(tracker = new LivePerformanceTracker
                {
                    BypassAutoSizeAxes = Axes.Both,
                });
            }

            tracker.Start();

            performance.BindTo(tracker.Performance);
            trackerValid.BindTo(tracker.IsValid);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            performance.BindValueChanged(p => Current.Value = (int)Math.Round(p.NewValue, MidpointRounding.AwayFromZero), true);
            trackerValid.BindValueChanged(v => IsValid = v.NewValue, true);
        }

        public virtual bool IsValid { get; set; }
    }
}
