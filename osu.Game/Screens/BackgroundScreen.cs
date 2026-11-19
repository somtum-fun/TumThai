// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Screens;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Performance;
using osuTK;

namespace osu.Game.Screens
{
    public abstract partial class BackgroundScreen : Screen, IEquatable<BackgroundScreen>
    {
        public const float TRANSITION_LENGTH = 500;

        private const float x_movement_amount = 50;

        private static float transitionLength => PotatoMode.UltraActive ? 0 : TRANSITION_LENGTH;

        public override bool IsPresent => base.IsPresent || Scheduler.HasPendingTasks;

        public bool AnimateEntry { get; set; } = true;

        protected BackgroundScreen()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        public virtual bool Equals(BackgroundScreen other)
        {
            return other?.GetType() == GetType();
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // we don't want to handle escape key.
            return false;
        }

        /// <summary>
        /// Apply arbitrary changes to this background in a thread safe manner.
        /// </summary>
        /// <param name="action">The operation to perform.</param>
        public void ApplyToBackground(Action<BackgroundScreen> action) => Schedule(() => action.Invoke(this));

        protected override void Update()
        {
            base.Update();
            Scale = PotatoMode.UltraActive ? Vector2.One : new Vector2(1 + x_movement_amount / DrawSize.X * 2);
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            if (AnimateEntry && !PotatoMode.UltraActive)
            {
                this.FadeOut();
                this.FadeIn(transitionLength, Easing.InOutQuart);
                this.MoveToX(x_movement_amount);
                this.MoveToX(0, transitionLength, Easing.InOutQuart);
            }

            base.OnEntering(e);
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            if (!PotatoMode.UltraActive)
                this.MoveToX(-x_movement_amount, transitionLength, Easing.InOutQuart);
            base.OnSuspending(e);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (IsLoaded)
            {
                this.FadeOut(transitionLength, Easing.OutExpo);
                if (!PotatoMode.UltraActive)
                    this.MoveToX(x_movement_amount, transitionLength, Easing.OutExpo);
            }

            return base.OnExiting(e);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            if (IsLoaded)
                this.MoveToX(0, transitionLength, Easing.OutExpo);
            base.OnResuming(e);
        }
    }
}
