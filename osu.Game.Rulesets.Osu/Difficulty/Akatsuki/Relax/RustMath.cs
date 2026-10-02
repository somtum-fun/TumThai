// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// osu!somtum: Akatsuki's Relax pp (akatsuki-pp-rs osu_2019 @ 591de0d), ported 1:1.

using System;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Akatsuki.Relax
{
    /// <summary>
    /// Helpers that reproduce the exact semantics of the Rust float operations used by the reference implementation.
    /// </summary>
    internal static class RustMath
    {
        /// <summary>
        /// Rust's <c>f32::max</c>: a NaN operand is ignored.
        /// </summary>
        public static float Max(float a, float b)
        {
            if (float.IsNaN(a)) return b;
            if (float.IsNaN(b)) return a;

            return a > b ? a : b;
        }

        /// <summary>
        /// Rust's <c>f32::min</c>: a NaN operand is ignored.
        /// </summary>
        public static float Min(float a, float b)
        {
            if (float.IsNaN(a)) return b;
            if (float.IsNaN(b)) return a;

            return a < b ? a : b;
        }

        /// <summary>
        /// Rust's <c>f64::max</c>: a NaN operand is ignored.
        /// </summary>
        public static double Max(double a, double b)
        {
            if (double.IsNaN(a)) return b;
            if (double.IsNaN(b)) return a;

            return a > b ? a : b;
        }

        /// <summary>
        /// Rust's <c>f64::min</c>: a NaN operand is ignored.
        /// </summary>
        public static double Min(double a, double b)
        {
            if (double.IsNaN(a)) return b;
            if (double.IsNaN(b)) return a;

            return a < b ? a : b;
        }

        /// <summary>
        /// Rust's <c>f32::powi</c> (compiler-rt <c>__powisf2</c>: square-and-multiply).
        /// </summary>
        public static float Powi(float a, int b)
        {
            bool recip = b < 0;
            float r = 1;

            while (true)
            {
                if ((b & 1) != 0)
                    r *= a;

                b /= 2;

                if (b == 0)
                    break;

                a *= a;
            }

            return recip ? 1 / r : r;
        }

        /// <summary>
        /// <c>rosu_map::util::Pos::length</c>.
        /// </summary>
        public static float Length(Vector2 v) => (float)Math.Sqrt(v.X * v.X + v.Y * v.Y);

        /// <summary>
        /// <c>rosu_map::util::Pos::normalize</c>.
        /// </summary>
        public static Vector2 Normalize(Vector2 v)
        {
            float scale = 1 / Length(v);
            return new Vector2(v.X * scale, v.Y * scale);
        }

        /// <summary>
        /// <c>rosu_map::util::Pos::dot</c>.
        /// </summary>
        public static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;
    }
}
