// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input;
using osu.Framework.Input.States;
using osu.Game.Rulesets.Replays;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Replays
{
    public class Replay : IDeepCloneable<Replay>
    {
        /// <summary>
        /// Whether all frames for this replay have been received.
        /// If false, gameplay would be paused to wait for further data, for instance.
        /// </summary>
        public bool HasReceivedAllFrames = true;

        public List<ReplayFrame> Frames = new List<ReplayFrame>();

        /// <summary>
        /// TumThai: gamefield-space multi-touch stream recorded for custom rulesets that consume <see cref="TouchState"/>
        /// directly without mapping touch to mouse or serialising touch actions in <see cref="ReplayFrame"/> (e.g. Sentakki).
        /// </summary>
        internal List<ReplayTouchFrame>? CustomTouchFrames;

        public Replay DeepClone()
        {
            return new Replay
            {
                HasReceivedAllFrames = HasReceivedAllFrames,
                // individual frames are mutable for now but hopefully this will not be a thing in the future.
                Frames = Frames.ToList(),
                CustomTouchFrames = CustomTouchFrames?.ToList(),
            };
        }
    }

    internal readonly record struct ReplayTouchPoint(TouchSource Source, Vector2 Position);

    internal sealed class ReplayTouchFrame
    {
        public readonly double Time;
        public readonly ReplayTouchPoint[] Touches;

        public ReplayTouchFrame(double time, ReplayTouchPoint[] touches)
        {
            Time = time;
            Touches = touches;
        }
    }
}
