// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: Akatsuki's Relax pp (akatsuki-pp-rs osu_2019 @ 591de0d), ported 1:1.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Relax
{
    /// <summary>
    /// Port of <c>osu_2019::osu_object::OsuObject</c>: a hit object with its lazily computed slider end position and travel distance.
    /// Stack leniency is not considered and positions are the ones from the .osu file (no Hard Rock flip).
    /// </summary>
    internal class AkatsukiRelaxObject
    {
        private const double legacy_last_tick_offset = 36.0;
        private const double base_scoring_distance = 100.0;

        /// <summary>
        /// Stable applied a 24ms offset to beatmaps older than v5, which lazer's decoder adds to all times. The Rust parser doesn't.
        /// </summary>
        private const int early_version_timing_offset = 24;

        public readonly float Time;
        public readonly Vector2 Pos;
        public readonly Vector2 EndPos;

        /// <summary>
        /// Circle: 0, slider: the travel distance, spinner: <c>null</c>.
        /// </summary>
        public readonly float? TravelDist;

        /// <summary>
        /// The combo this object contributes (head, ticks, repeats and tail).
        /// </summary>
        public readonly int Combo;

        public bool IsSpinner => TravelDist == null;

        public AkatsukiRelaxObject(OsuHitObject h, IBeatmap map, float radius, float scalingFactor, bool hardRock)
        {
            double startTime = h.StartTime - (map.BeatmapVersion < 5 ? early_version_timing_offset : 0);

            // Undo Hard Rock's vertical flip, the Rust calculator works on the unmodified positions.
            Vector2 pos = hardRock ? new Vector2(h.Position.X, OsuPlayfield.BASE_SIZE.Y - h.Position.Y) : h.Position;

            Time = (float)startTime;
            Pos = pos;
            Combo = 1; // hitcircle, slider head, or spinner

            switch (h)
            {
                case Slider slider:
                {
                    double beatLen = map.ControlPointInfo.TimingPointAt(slider.StartTime).BeatLength;
                    double sliderVel = slider.SliderVelocityMultiplier;
                    bool generateTicks = slider.GenerateTicks;

                    double scoringDist = base_scoring_distance * map.Difficulty.SliderMultiplier * sliderVel;
                    double vel = scoringDist / beatLen;

                    Vector2 endPos = pos;
                    float travelDist = 0;
                    int combo = 1;

                    float approxFollowCircleRadius = radius * 3;

                    double tickDistMult = map.BeatmapVersion < 8 ? 1.0 / sliderVel : 1.0;

                    double tickDist = generateTicks
                        ? scoringDist / map.Difficulty.SliderTickRate * tickDistMult
                        : double.PositiveInfinity;

                    int repeats = slider.RepeatCount;
                    double spanCount = repeats + 1;

                    var path = slider.Path;
                    double curveDist = path.Distance;

                    double endTime = startTime + spanCount * curveDist / vel;
                    double totalDuration = endTime - startTime;
                    double spanDuration = totalDuration / spanCount;

                    // Called on each slider object except for the head.
                    // Increases combo and adjusts `endPos` and `travelDist`
                    // w.r.t. the object position at the given time on the slider curve.
                    void computeVertex(double time)
                    {
                        combo++;

                        double progress = (time - startTime) / spanDuration;

                        if (progress % 2.0 >= 1.0)
                            progress = 1.0 - progress % 1.0;
                        else
                            progress %= 1.0;

                        Vector2 offset = path.PositionAt(progress);
                        if (hardRock)
                            offset.Y = -offset.Y;

                        Vector2 currPos = pos + offset;

                        Vector2 diff = currPos - endPos;
                        float dist = RustMath.Length(diff);

                        if (dist > approxFollowCircleRadius)
                        {
                            dist -= approxFollowCircleRadius;
                            endPos += RustMath.Normalize(diff) * dist;
                            travelDist += dist;
                        }
                    }

                    const double max_len = 100_000.0;

                    double len = RustMath.Min(curveDist, max_len);
                    tickDist = clamp(tickDist, 0.0, len);
                    double minDistFromEnd = vel * 10.0;

                    double currDist = tickDist;

                    if (tickDist != 0.0)
                    {
                        var ticks = new List<double>();

                        // Tick of the first span
                        while (currDist < len - minDistFromEnd)
                        {
                            double progress = currDist / len;

                            double currTime = startTime + progress * spanDuration;
                            computeVertex(currTime);
                            ticks.Add(currTime);

                            currDist += tickDist;
                        }

                        // Other spans
                        for (int spanIdx = 1; spanIdx <= repeats; spanIdx++)
                        {
                            double spanIdxF64 = spanIdx;

                            // Repeat point
                            double currTime = startTime + spanDuration * spanIdxF64;
                            computeVertex(currTime);

                            double spanOffset = spanIdxF64 * spanDuration;

                            // Ticks
                            if ((spanIdx & 1) == 1)
                            {
                                double @base = startTime + startTime + spanDuration;

                                for (int i = ticks.Count - 1; i >= 0; i--)
                                    computeVertex(spanOffset + @base - ticks[i]);
                            }
                            else
                            {
                                foreach (double time in ticks)
                                    computeVertex(spanOffset + time);
                            }
                        }
                    }

                    // Slider tail
                    double finalSpanStartTime = startTime + repeats * spanDuration;
                    double finalSpanEndTime = RustMath.Max(startTime + totalDuration / 2.0, finalSpanStartTime + spanDuration - legacy_last_tick_offset);
                    computeVertex(finalSpanEndTime);

                    travelDist *= scalingFactor;

                    EndPos = endPos;
                    TravelDist = travelDist;
                    Combo = combo;
                    break;
                }

                case Spinner:
                    EndPos = pos;
                    TravelDist = null;
                    break;

                default:
                    EndPos = pos;
                    TravelDist = 0;
                    break;
            }
        }

        /// <summary>
        /// Rust's <c>f64::clamp</c> (NaN stays NaN).
        /// </summary>
        private static double clamp(double value, double min, double max)
        {
            if (value < min) value = min;
            if (value > max) value = max;
            return value;
        }
    }
}
