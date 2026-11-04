// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Touch input mapper implementing osu!droid's 3-cursor multi-touch input, hitting, slider tracking, and spinner mechanics.
    /// </summary>
    public partial class OsuDroidTouchInputMapper : Drawable
    {
        /// <summary>
        /// Maximum simultaneous active cursors in osu!droid (<c>GameScene.maximumActiveCursorCount</c>).
        /// </summary>
        public const int MAX_ACTIVE_CURSORS = 3;

        private const float drag_claim_distance = 15f;

        private readonly List<TrackedTouch> trackedTouches = new List<TrackedTouch>(MAX_ACTIVE_CURSORS);

        private readonly OsuInputManager osuInputManager;
        private readonly OsuPlayfield playfield;

        private TrackedTouch? primaryCursorTouch;
        private TrackedTouch? sliderTrackingTouch;
        private OsuAction? lastTriggeredAction;

        private Bindable<bool> tapsDisabled = null!;

        internal bool HasActiveTouches => trackedTouches.Count > 0;

        public OsuDroidTouchInputMapper(OsuInputManager inputManager, OsuPlayfield playfield)
        {
            osuInputManager = inputManager;
            this.playfield = playfield;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            tapsDisabled = config.GetBindable<bool>(OsuSetting.TouchDisableGameplayTaps);
        }

        // Required to handle touches outside of the playfield when screen scaling is enabled.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        /// <summary>
        /// Updates the active cursor position right before <see cref="OsuModRelax"/> evaluates the playfield,
        /// allowing up to <see cref="MAX_ACTIVE_CURSORS"/> held fingers to auto-hit circles, track sliders, and spin spinners.
        /// </summary>
        internal void UpdateRelaxCursor(double currentTime)
        {
            if (trackedTouches.Count == 0)
            {
                sliderTrackingTouch = null;
                return;
            }

            foreach (var drawable in playfield.HitObjectContainer.AliveObjects)
            {
                DrawableHitCircle? circle = drawable switch
                {
                    DrawableHitCircle hitCircle => hitCircle,
                    DrawableSlider slider => slider.HeadCircle,
                    _ => null,
                };

                if (circle == null || circle.IsHit || !circle.HitArea.CanBeHit())
                    continue;

                double timeOffset = currentTime - circle.HitObject.StartTime;

                if (timeOffset < -OsuModRelax.RELAX_LENIENCY)
                    break;

                HitResult result = circle.HitObject.HitWindows.ResultFor(timeOffset);

                if (result == HitResult.None || playfield.HitPolicy.CheckHittable(circle, currentTime, result) != ClickAction.Hit)
                    continue;

                if (primaryCursorTouch != null
                    && trackedTouches.Contains(primaryCursorTouch)
                    && circle.HitArea.ReceivePositionalInputAt(primaryCursorTouch.ScreenSpacePosition))
                {
                    osuInputManager.ApplyTouchPosition(primaryCursorTouch.ScreenSpacePosition);
                    return;
                }

                for (int i = 0; i < trackedTouches.Count; i++)
                {
                    TrackedTouch candidate = trackedTouches[i];

                    if (circle.HitArea.ReceivePositionalInputAt(candidate.ScreenSpacePosition))
                    {
                        primaryCursorTouch = candidate;
                        osuInputManager.ApplyTouchPosition(candidate.ScreenSpacePosition);
                        return;
                    }
                }
            }

            TrackedTouch? activeSliderTouch = findActiveSliderTouch(currentTime);
            sliderTrackingTouch = activeSliderTouch;

            if (activeSliderTouch != null)
            {
                primaryCursorTouch = activeSliderTouch;
                osuInputManager.ApplyTouchPosition(activeSliderTouch.ScreenSpacePosition);
                return;
            }

            if (isAnySpinnerActive())
            {
                TrackedTouch spinTouch = trackedTouches[0];
                primaryCursorTouch = spinTouch;
                osuInputManager.ApplyTouchPosition(spinTouch.ScreenSpacePosition);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (trackedTouches.Count == 0)
            {
                sliderTrackingTouch = null;
                return;
            }

            if (!osuInputManager.AllowGameplayInputs)
                return;

            bool canPressAction = !tapsDisabled.Value;

            // Match GameplaySlider.updateTracking: keep tracking with the active slider cursor, or latch onto any held cursor inside the follow area.
            TrackedTouch? activeSliderTouch = findActiveSliderTouch(Time.Current);
            sliderTrackingTouch = activeSliderTouch;

            if (activeSliderTouch != null)
            {
                primaryCursorTouch = activeSliderTouch;
                osuInputManager.ApplyTouchPosition(activeSliderTouch.ScreenSpacePosition);

                if (canPressAction && !isAnyActionPressed())
                {
                    OsuAction holdAction = getNextPreferredAction();
                    activeSliderTouch.Action = holdAction;
                    activeSliderTouch.ActionHeldForObject = true;
                    lastTriggeredAction = holdAction;
                    osuInputManager.KeyBindingContainer.TriggerPressed(holdAction);
                }

                return;
            }

            // Match GameplaySpinner.update: first active held cursor drives spinner rotation around playfield centre.
            if (isAnySpinnerActive())
            {
                TrackedTouch spinTouch = trackedTouches[0];
                primaryCursorTouch = spinTouch;
                osuInputManager.ApplyTouchPosition(spinTouch.ScreenSpacePosition);

                if (canPressAction && !isAnyActionPressed())
                {
                    OsuAction spinAction = getNextPreferredAction();
                    spinTouch.Action = spinAction;
                    spinTouch.ActionHeldForObject = true;
                    lastTriggeredAction = spinAction;
                    osuInputManager.KeyBindingContainer.TriggerPressed(spinAction);
                }

                return;
            }

            // Release any action that was kept held specifically for a finished slider or spinner.
            for (int i = 0; i < trackedTouches.Count; i++)
            {
                TrackedTouch touch = trackedTouches[i];

                if (touch.ActionHeldForObject)
                {
                    if (touch.Action is OsuAction heldAction)
                        osuInputManager.KeyBindingContainer.TriggerReleased(heldAction);

                    touch.Action = null;
                    touch.ActionHeldForObject = false;
                }
            }
        }

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            if (trackedTouches.Count >= MAX_ACTIVE_CURSORS)
                return true;

            bool canPressAction = osuInputManager.AllowGameplayInputs && !tapsDisabled.Value;

            var newTouch = new TrackedTouch(e.Touch.Source, e.ScreenSpaceTouchDownPosition);
            trackedTouches.Add(newTouch);

            Vector2 cursorPosition = resolveTouchDownCursorPosition(newTouch);
            osuInputManager.ApplyTouchPosition(cursorPosition);

            if (canPressAction)
            {
                if (!isAnySliderTracking() && !isAnySpinnerActive())
                    releaseOtherIdleActions(newTouch);

                OsuAction action = claimActionFor(newTouch);
                newTouch.Action = action;
                lastTriggeredAction = action;
                osuInputManager.KeyBindingContainer.TriggerPressed(action);
            }

            return true;
        }

        protected override void OnTouchMove(TouchMoveEvent e)
        {
            base.OnTouchMove(e);

            var tracked = findTrackedTouch(e.Touch.Source);
            if (tracked == null)
                return;

            tracked.ScreenSpacePosition = e.ScreenSpaceTouch.Position;
            tracked.DistanceTravelled += e.Delta.Length;

            TrackedTouch? activeSliderTouch = findActiveSliderTouch(Time.Current);
            sliderTrackingTouch = activeSliderTouch;

            if (activeSliderTouch != null)
            {
                if (tracked == activeSliderTouch)
                    osuInputManager.ApplyTouchPosition(tracked.ScreenSpacePosition);

                return;
            }

            if (isAnySpinnerActive())
            {
                if (tracked == trackedTouches[0])
                    osuInputManager.ApplyTouchPosition(tracked.ScreenSpacePosition);

                return;
            }

            if (primaryCursorTouch == null || (primaryCursorTouch != tracked && tracked.DistanceTravelled >= drag_claim_distance))
                primaryCursorTouch = tracked;

            if (tracked == primaryCursorTouch)
                osuInputManager.ApplyTouchPosition(tracked.ScreenSpacePosition);
        }

        protected override void OnTouchUp(TouchUpEvent e)
        {
            var tracked = findTrackedTouch(e.Touch.Source);

            if (tracked != null)
            {
                trackedTouches.Remove(tracked);

                if (sliderTrackingTouch == tracked)
                    sliderTrackingTouch = null;

                if (primaryCursorTouch == tracked)
                    primaryCursorTouch = trackedTouches.Count > 0 ? trackedTouches[^1] : null;

                if (tracked.Action is OsuAction action)
                {
                    TrackedTouch? handoffTouch = findActiveSliderTouch(Time.Current) ?? (isAnySpinnerActive() && trackedTouches.Count > 0 ? trackedTouches[0] : null);

                    if (handoffTouch != null && handoffTouch.Action == null)
                    {
                        handoffTouch.Action = action;
                        handoffTouch.ActionHeldForObject = true;
                        primaryCursorTouch = handoffTouch;
                        osuInputManager.ApplyTouchPosition(handoffTouch.ScreenSpacePosition);
                    }
                    else
                    {
                        osuInputManager.KeyBindingContainer.TriggerReleased(action);
                    }
                }
            }

            base.OnTouchUp(e);
        }

        /// <summary>
        /// Resolves the screen-space cursor position for a new touch-down event, matching <c>GameObject.getHittingCursor</c>:
        /// checks the newly pressed cursor first, then any other currently held cursor against judgeable circles in start-time order.
        /// </summary>
        private Vector2 resolveTouchDownCursorPosition(TrackedTouch newTouch)
        {
            Vector2? directEarlyPosition = null;

            foreach (var drawable in playfield.HitObjectContainer.AliveObjects)
            {
                DrawableHitCircle? circle = drawable switch
                {
                    DrawableHitCircle hitCircle => hitCircle,
                    DrawableSlider slider => slider.HeadCircle,
                    _ => null,
                };

                if (circle == null || !circle.HitArea.CanBeHit())
                    continue;

                double timeOffset = Time.Current - circle.HitObject.StartTime;

                if (timeOffset < -OsuHitWindows.MISS_WINDOW)
                    break;

                HitResult result = circle.HitObject.HitWindows.ResultFor(timeOffset);

                if (result != HitResult.None && playfield.HitPolicy.CheckHittable(circle, Time.Current, result) == ClickAction.Hit)
                {
                    if (circle.HitArea.ReceivePositionalInputAt(newTouch.ScreenSpacePosition))
                    {
                        primaryCursorTouch = newTouch;
                        return newTouch.ScreenSpacePosition;
                    }

                    for (int i = 0; i < trackedTouches.Count; i++)
                    {
                        TrackedTouch other = trackedTouches[i];

                        if (other == newTouch)
                            continue;

                        if (circle.HitArea.ReceivePositionalInputAt(other.ScreenSpacePosition))
                        {
                            primaryCursorTouch = other;
                            return other.ScreenSpacePosition;
                        }
                    }
                }
                else if (directEarlyPosition == null && circle.HitArea.ReceivePositionalInputAt(newTouch.ScreenSpacePosition))
                {
                    directEarlyPosition = newTouch.ScreenSpacePosition;
                }
            }

            // Keep the cursor on any active slider-tracking finger if this tap did not hit a new object.
            TrackedTouch? activeSliderTouch = findActiveSliderTouch(Time.Current);

            if (activeSliderTouch != null)
            {
                sliderTrackingTouch = activeSliderTouch;
                return activeSliderTouch.ScreenSpacePosition;
            }

            if (directEarlyPosition is Vector2 earlyPos)
            {
                primaryCursorTouch = newTouch;
                return earlyPos;
            }

            if (!osuInputManager.AllowGameplayInputs)
            {
                primaryCursorTouch = newTouch;
                return newTouch.ScreenSpacePosition;
            }

            primaryCursorTouch ??= newTouch;
            return primaryCursorTouch.ScreenSpacePosition;
        }

        private void releaseOtherIdleActions(TrackedTouch currentTouch)
        {
            for (int i = 0; i < trackedTouches.Count; i++)
            {
                TrackedTouch other = trackedTouches[i];

                if (other == currentTouch || other.Action is not OsuAction action)
                    continue;

                osuInputManager.KeyBindingContainer.TriggerReleased(action);
                other.Action = null;
                other.ActionHeldForObject = false;
            }
        }

        private OsuAction claimActionFor(TrackedTouch newTouch)
        {
            OsuAction preferred = getNextPreferredAction();
            OsuAction alternate = preferred == OsuAction.LeftButton ? OsuAction.RightButton : OsuAction.LeftButton;

            if (!isActionPressed(preferred))
                return preferred;

            if (!isActionPressed(alternate))
                return alternate;

            // Both actions are held (3 overlapping touches during an active slider/spinner).
            // Reclaim the non-tracking touch's action so the active slider/spinner hold is preserved.
            TrackedTouch? protectedTouch = sliderTrackingTouch ?? primaryCursorTouch;
            TrackedTouch? victim = null;

            for (int i = 0; i < trackedTouches.Count; i++)
            {
                var candidate = trackedTouches[i];

                if (candidate == newTouch || candidate.Action == null)
                    continue;

                if (candidate != protectedTouch)
                {
                    victim = candidate;
                    break;
                }

                victim ??= candidate;
            }

            if (victim?.Action is OsuAction reclaimed)
            {
                osuInputManager.KeyBindingContainer.TriggerReleased(reclaimed);
                victim.Action = null;
                victim.ActionHeldForObject = false;
                return reclaimed;
            }

            return preferred;
        }

        private OsuAction getNextPreferredAction()
            => lastTriggeredAction == OsuAction.LeftButton ? OsuAction.RightButton : OsuAction.LeftButton;

        private TrackedTouch? findTrackedTouch(TouchSource source)
        {
            for (int i = 0; i < trackedTouches.Count; i++)
            {
                if (trackedTouches[i].Source == source)
                    return trackedTouches[i];
            }

            return null;
        }

        private bool isActionPressed(OsuAction action)
        {
            for (int i = 0; i < trackedTouches.Count; i++)
            {
                if (trackedTouches[i].Action == action)
                    return true;
            }

            return false;
        }

        private bool isAnyActionPressed()
        {
            for (int i = 0; i < trackedTouches.Count; i++)
            {
                if (trackedTouches[i].Action != null)
                    return true;
            }

            return false;
        }

        private TrackedTouch? findActiveSliderTouch(double currentTime)
        {
            foreach (var drawable in playfield.HitObjectContainer.AliveEntries.Values)
            {
                if (drawable is not DrawableSlider slider || slider.AllJudged)
                    continue;

                if (!slider.HeadCircle.Judged && currentTime < slider.HitObject.StartTime)
                    continue;

                if (currentTime > slider.HitObject.EndTime)
                    continue;

                if (sliderTrackingTouch != null
                    && trackedTouches.Contains(sliderTrackingTouch)
                    && isPositionInSliderFollowArea(slider, sliderTrackingTouch.ScreenSpacePosition, currentTime))
                {
                    return sliderTrackingTouch;
                }

                for (int i = 0; i < trackedTouches.Count; i++)
                {
                    if (isPositionInSliderFollowArea(slider, trackedTouches[i].ScreenSpacePosition, currentTime))
                        return trackedTouches[i];
                }
            }

            return null;
        }

        private static bool isPositionInSliderFollowArea(DrawableSlider slider, Vector2 screenSpacePosition, double currentTime)
        {
            float radius = (float)slider.HitObject.Radius;

            if (slider.SliderInputManager.Tracking)
                radius *= slider.HitObject.FollowCircleArea;

            double followProgress = Math.Clamp((currentTime - slider.HitObject.StartTime) / slider.HitObject.Duration, 0, 1);
            Vector2 followCirclePosition = slider.HitObject.CurvePositionAt(followProgress);
            Vector2 positionInSlider = slider.ToLocalSpace(screenSpacePosition) - slider.OriginPosition;

            return (positionInSlider - followCirclePosition).LengthSquared <= radius * radius;
        }

        private bool isAnySliderTracking()
        {
            foreach (var drawable in playfield.HitObjectContainer.AliveEntries.Values)
            {
                if (drawable is DrawableSlider slider && slider.SliderInputManager.Tracking)
                    return true;
            }

            return false;
        }

        private bool isAnySpinnerActive()
        {
            foreach (var drawable in playfield.HitObjectContainer.AliveEntries.Values)
            {
                if (drawable is DrawableSpinner spinner && spinner.RotationTracker.IsSpinnableTime && !spinner.Result.HasResult)
                    return true;
            }

            return false;
        }

        private class TrackedTouch
        {
            public readonly TouchSource Source;

            public Vector2 ScreenSpacePosition;
            public OsuAction? Action;
            public bool ActionHeldForObject;
            public float DistanceTravelled;

            public TrackedTouch(TouchSource source, Vector2 initialScreenSpacePosition)
            {
                Source = source;
                ScreenSpacePosition = initialScreenSpacePosition;
            }
        }
    }
}
