// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: ppy's 2024.1115.0 osu! difficulty algorithm, kept for Akatsuki Autopilot pp (akatsuki-pp-rs).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Preprocessing
{
    public class AkatsukiOsuDifficultyHitObject : DifficultyHitObject
    {
        /// <summary>
        /// A distance by which all distances should be scaled in order to assume a uniform circle size.
        /// </summary>
        public const int NORMALISED_RADIUS = 50; // Change radius to 50 to make 100 the diameter. Easier for mental maths.

        public const int MIN_DELTA_TIME = 25;

        private const float maximum_slider_radius = NORMALISED_RADIUS * 2.4f;
        private const float assumed_slider_radius = NORMALISED_RADIUS * 1.8f;

        protected new OsuHitObject BaseObject => (OsuHitObject)base.BaseObject;

        /// <summary>
        /// Milliseconds elapsed since the start time of the previous <see cref="AkatsukiOsuDifficultyHitObject"/>, with a minimum of 25ms.
        /// </summary>
        public readonly double StrainTime;

        /// <summary>
        /// Normalised distance from the "lazy" end position of the previous <see cref="AkatsukiOsuDifficultyHitObject"/> to the start position of this <see cref="AkatsukiOsuDifficultyHitObject"/>.
        /// </summary>
        public double LazyJumpDistance { get; private set; }

        /// <summary>
        /// Normalised shortest distance to consider for a jump between the previous <see cref="AkatsukiOsuDifficultyHitObject"/> and this <see cref="AkatsukiOsuDifficultyHitObject"/>.
        /// </summary>
        public double MinimumJumpDistance { get; private set; }

        /// <summary>
        /// The time taken to travel through <see cref="MinimumJumpDistance"/>, with a minimum value of 25ms.
        /// </summary>
        public double MinimumJumpTime { get; private set; }

        /// <summary>
        /// Normalised distance between the start and end position of this <see cref="AkatsukiOsuDifficultyHitObject"/>.
        /// </summary>
        public double TravelDistance { get; private set; }

        /// <summary>
        /// The time taken to travel through <see cref="TravelDistance"/>, with a minimum value of 25ms for <see cref="Slider"/> objects.
        /// </summary>
        public double TravelTime { get; private set; }

        /// <summary>
        /// Angle the player has to take to hit this <see cref="AkatsukiOsuDifficultyHitObject"/>.
        /// Calculated as the angle between the circles (current-2, current-1, current).
        /// </summary>
        public double? Angle { get; private set; }

        /// <summary>
        /// The full rate-adjusted hit window for a Great, as 2024.1115.0 computed it (80 - 6 * OD, not floored like the current <see cref="Scoring.OsuHitWindows"/>).
        /// </summary>
        public double GreatHitWindow { get; private set; }

        /// <summary>
        /// The "lazy" travel distance through this object, if it's a slider (<c>Slider.LazyTravelDistance</c> in 2024.1115.0).
        /// </summary>
        public double SliderLazyTravelDistance => BaseObject is Slider slider ? getLazyData(slider).LazyTravelDistance : 0;

        private readonly OsuHitObject? lastLastObject;
        private readonly OsuHitObject lastObject;

        private readonly double timePreempt;

        public AkatsukiOsuDifficultyHitObject(HitObject hitObject, HitObject lastObject, HitObject? lastLastObject, double clockRate, double overallDifficulty, double approachRate,
                                              List<DifficultyHitObject> objects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            this.lastLastObject = lastLastObject as OsuHitObject;
            this.lastObject = (OsuHitObject)lastObject;

            // Capped to 25ms to prevent difficulty calculation breaking from simultaneous objects.
            StrainTime = Math.Max(DeltaTime, MIN_DELTA_TIME);

            // osu!somtum: the hit windows have since changed to floor(...) - 0.5; keep the 2024.1115.0 (and akatsuki-pp-rs) window.
            GreatHitWindow = 2 * IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, 80, 50, 20) / clockRate;

            // osu!somtum: the current hit objects round the preempt and keep the default fade-in on sliders under Hidden;
            // 2024.1115.0 (and akatsuki-pp-rs) used the unrounded preempt and the Hidden fade-in for every object.
            timePreempt = IBeatmapDifficultyInfo.DifficultyRange(approachRate, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN);

            setDistances(clockRate);
        }

        public double OpacityAt(double time, bool hidden)
        {
            if (time > BaseObject.StartTime)
            {
                // Consider a hitobject as being invisible when its start time is passed.
                // In reality the hitobject will be visible beyond its start time up until its hittable window has passed,
                // but this is an approximation and such a case is unlikely to be hit where this function is used.
                return 0.0;
            }

            double timeFadeIn = hidden
                ? timePreempt * OsuModHidden.FADE_IN_DURATION_MULTIPLIER
                : 400 * Math.Min(1, timePreempt / OsuHitObject.PREEMPT_MIN);

            double fadeInStartTime = BaseObject.StartTime - timePreempt;
            double fadeInDuration = timeFadeIn;

            if (hidden)
            {
                // Taken from OsuModHidden.
                double fadeOutStartTime = BaseObject.StartTime - timePreempt + timeFadeIn;
                double fadeOutDuration = timePreempt * OsuModHidden.FADE_OUT_DURATION_MULTIPLIER;

                return Math.Min
                (
                    Math.Clamp((time - fadeInStartTime) / fadeInDuration, 0.0, 1.0),
                    1.0 - Math.Clamp((time - fadeOutStartTime) / fadeOutDuration, 0.0, 1.0)
                );
            }

            return Math.Clamp((time - fadeInStartTime) / fadeInDuration, 0.0, 1.0);
        }

        /// <summary>
        /// Returns how possible is it to doubletap this object together with the next one and get perfect judgement in range from 0 to 1
        /// </summary>
        public double GetDoubletapness(AkatsukiOsuDifficultyHitObject? osuNextObj)
        {
            if (osuNextObj != null)
            {
                double currDeltaTime = Math.Max(1, DeltaTime);
                double nextDeltaTime = Math.Max(1, osuNextObj.DeltaTime);
                double deltaDifference = Math.Abs(nextDeltaTime - currDeltaTime);
                double speedRatio = currDeltaTime / Math.Max(currDeltaTime, deltaDifference);
                double windowRatio = Math.Pow(Math.Min(1, currDeltaTime / GreatHitWindow), 2);
                return 1.0 - Math.Pow(speedRatio, 1 - windowRatio);
            }

            return 0;
        }

        private void setDistances(double clockRate)
        {
            if (BaseObject is Slider currentSlider)
            {
                var lazy = computeSliderCursorPosition(currentSlider);
                // Bonus for repeat sliders until a better per nested object strain system can be achieved.
                TravelDistance = lazy.LazyTravelDistance * (float)Math.Pow(1 + currentSlider.RepeatCount / 2.5, 1.0 / 2.5);
                TravelTime = Math.Max(lazy.LazyTravelTime / clockRate, MIN_DELTA_TIME);
            }

            // We don't need to calculate either angle or distance when one of the last->curr objects is a spinner
            if (BaseObject is Spinner || lastObject is Spinner)
                return;

            // We will scale distances by this factor, so we can assume a uniform CircleSize among beatmaps.
            float scalingFactor = NORMALISED_RADIUS / (float)BaseObject.Radius;

            if (BaseObject.Radius < 30)
            {
                float smallCircleBonus = Math.Min(30 - (float)BaseObject.Radius, 5) / 50;
                scalingFactor *= 1 + smallCircleBonus;
            }

            Vector2 lastCursorPosition = getEndCursorPosition(lastObject);

            LazyJumpDistance = (BaseObject.StackedPosition * scalingFactor - lastCursorPosition * scalingFactor).Length;
            MinimumJumpTime = StrainTime;
            MinimumJumpDistance = LazyJumpDistance;

            if (lastObject is Slider lastSlider)
            {
                double lastTravelTime = Math.Max(computeSliderCursorPosition(lastSlider).LazyTravelTime / clockRate, MIN_DELTA_TIME);
                MinimumJumpTime = Math.Max(StrainTime - lastTravelTime, MIN_DELTA_TIME);

                // There are two types of slider-to-object patterns to consider in order to better approximate the real movement a player will take to jump between the hitobjects.
                // 1. The anti-flow pattern, where players cut the slider short in order to move to the next hitobject (approximated by LazyJumpDistance).
                // 2. The flow pattern, where players follow through the slider to its visual extent into the next hitobject (approximated by "tailJumpDistance").
                // Thus, the player is assumed to jump the minimum of these two distances in all cases.

                float tailJumpDistance = Vector2.Subtract(lastSlider.TailCircle.StackedPosition, BaseObject.StackedPosition).Length * scalingFactor;
                MinimumJumpDistance = Math.Max(0, Math.Min(LazyJumpDistance - (maximum_slider_radius - assumed_slider_radius), tailJumpDistance - maximum_slider_radius));
            }

            if (lastLastObject != null && !(lastLastObject is Spinner))
            {
                Vector2 lastLastCursorPosition = getEndCursorPosition(lastLastObject);

                Vector2 v1 = lastLastCursorPosition - lastObject.StackedPosition;
                Vector2 v2 = BaseObject.StackedPosition - lastCursorPosition;

                float dot = Vector2.Dot(v1, v2);
                float det = v1.X * v2.Y - v1.Y * v2.X;

                Angle = Math.Abs(Math.Atan2(det, dot));
            }
        }

        /// <summary>
        /// osu!somtum: 2024.1115.0 stored these on <see cref="Slider"/> (LazyEndPosition, LazyTravelDistance, LazyTravelTime); the current
        /// <see cref="Slider"/> no longer has them, so they are kept here per slider instance.
        /// </summary>
        private class LazySliderData
        {
            public Vector2? LazyEndPosition;
            public float LazyTravelDistance;
            public double LazyTravelTime;
        }

        private static readonly ConditionalWeakTable<Slider, LazySliderData> lazy_slider_data = new ConditionalWeakTable<Slider, LazySliderData>();

        private static LazySliderData getLazyData(Slider slider) => lazy_slider_data.GetValue(slider, _ => new LazySliderData());

        private static LazySliderData computeSliderCursorPosition(Slider slider)
        {
            var lazy = getLazyData(slider);

            lock (lazy)
            {
                if (lazy.LazyEndPosition != null)
                    return lazy;

                // TODO: This commented version is actually correct by the new lazer implementation, but intentionally held back from
                // difficulty calculator to preserve known behaviour.
                // double trackingEndTime = Math.Max(
                //     // SliderTailCircle always occurs at the final end time of the slider, but the player only needs to hold until within a lenience before it.
                //     slider.Duration + SliderEventGenerator.TAIL_LENIENCY,
                //     // There's an edge case where one or more ticks/repeats fall within that leniency range.
                //     // In such a case, the player needs to track until the final tick or repeat.
                //     slider.NestedHitObjects.LastOrDefault(n => n is not SliderTailCircle)?.StartTime ?? double.MinValue
                // );

                double trackingEndTime = Math.Max(
                    slider.StartTime + slider.Duration + SliderEventGenerator.TAIL_LENIENCY,
                    slider.StartTime + slider.Duration / 2
                );

                IList<HitObject> nestedObjects = slider.NestedHitObjects;

                SliderTick? lastRealTick = null;

                foreach (var hitobject in slider.NestedHitObjects)
                {
                    if (hitobject is SliderTick tick)
                        lastRealTick = tick;
                }

                if (lastRealTick?.StartTime > trackingEndTime)
                {
                    trackingEndTime = lastRealTick.StartTime;

                    // When the last tick falls after the tracking end time, we need to re-sort the nested objects
                    // based on time. This creates a somewhat weird ordering which is counter to how a user would
                    // understand the slider, but allows a zero-diff with known diffcalc output.
                    List<HitObject> reordered = nestedObjects.ToList();

                    reordered.Remove(lastRealTick);
                    reordered.Add(lastRealTick);

                    nestedObjects = reordered;
                }

                lazy.LazyTravelTime = trackingEndTime - slider.StartTime;

                double endTimeMin = lazy.LazyTravelTime / slider.SpanDuration;
                if (endTimeMin % 2 >= 1)
                    endTimeMin = 1 - endTimeMin % 1;
                else
                    endTimeMin %= 1;

                Vector2 lazyEndPosition = slider.StackedPosition + slider.Path.PositionAt(endTimeMin); // temporary lazy end position until a real result can be derived.

                Vector2 currCursorPosition = slider.StackedPosition;

                double scalingFactor = NORMALISED_RADIUS / slider.Radius; // lazySliderDistance is coded to be sensitive to scaling, this makes the maths easier with the thresholds being used.

                for (int i = 1; i < nestedObjects.Count; i++)
                {
                    var currMovementObj = (OsuHitObject)nestedObjects[i];

                    Vector2 currMovement = Vector2.Subtract(currMovementObj.StackedPosition, currCursorPosition);
                    double currMovementLength = scalingFactor * currMovement.Length;

                    // Amount of movement required so that the cursor position needs to be updated.
                    double requiredMovement = assumed_slider_radius;

                    if (i == nestedObjects.Count - 1)
                    {
                        // The end of a slider has special aim rules due to the relaxed time constraint on position.
                        // There is both a lazy end position as well as the actual end slider position. We assume the player takes the simpler movement.
                        // For sliders that are circular, the lazy end position may actually be farther away than the sliders true end.
                        // This code is designed to prevent buffing situations where lazy end is actually a less efficient movement.
                        Vector2 lazyMovement = Vector2.Subtract(lazyEndPosition, currCursorPosition);

                        if (lazyMovement.Length < currMovement.Length)
                            currMovement = lazyMovement;

                        currMovementLength = scalingFactor * currMovement.Length;
                    }
                    else if (currMovementObj is SliderRepeat)
                    {
                        // For a slider repeat, assume a tighter movement threshold to better assess repeat sliders.
                        requiredMovement = NORMALISED_RADIUS;
                    }

                    if (currMovementLength > requiredMovement)
                    {
                        // this finds the positional delta from the required radius and the current position, and updates the currCursorPosition accordingly, as well as rewarding distance.
                        currCursorPosition = Vector2.Add(currCursorPosition, Vector2.Multiply(currMovement, (float)((currMovementLength - requiredMovement) / currMovementLength)));
                        currMovementLength *= (currMovementLength - requiredMovement) / currMovementLength;
                        lazy.LazyTravelDistance += (float)currMovementLength;
                    }

                    if (i == nestedObjects.Count - 1)
                        lazyEndPosition = currCursorPosition;
                }

                lazy.LazyEndPosition = lazyEndPosition;
                return lazy;
            }
        }

        private static Vector2 getEndCursorPosition(OsuHitObject hitObject)
        {
            Vector2 pos = hitObject.StackedPosition;

            if (hitObject is Slider slider)
                pos = computeSliderCursorPosition(slider).LazyEndPosition ?? pos;

            return pos;
        }
    }
}
