// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osuTK;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// TumThai!Next: the osu!stable theme's menu cursor trail, drawn by the same <see cref="CursorTrail"/> as gameplay and
    /// following the gameplay legacy trail's rules (osu.Game.Rulesets.Osu's LegacyCursorTrail): a "disjoint" trail, one
    /// part every 1/60 s at the cursor, when the cursor's skin has no cursormiddle, otherwise a smooth additive trail.
    /// </summary>
    public partial class StableMenuCursorTrail : CursorTrail
    {
        private const double disjoint_trail_time_separation = 1000 / 60.0;

        private bool disjointTrail;
        private bool hasTexture;
        private double lastTrailTime;
        private Vector2? currentPosition;

        /// <summary>
        /// The gameplay cursor size setting (the smooth trail's part spacing follows it, as in gameplay).
        /// </summary>
        public float CursorSizeSetting { get; set; } = 1;

        /// <summary>
        /// Sets the trail up for a skin, as LegacyCursorTrail does in gameplay.
        /// </summary>
        /// <param name="texture">The skin's cursortrail.</param>
        /// <param name="disjoint">Whether the skin that provides the cursor has no cursormiddle.</param>
        /// <param name="centre">skin.ini CursorCentre.</param>
        /// <param name="rotate">skin.ini CursorTrailRotate.</param>
        public void Apply(Texture? texture, bool disjoint, bool centre, bool rotate)
        {
            // never null: the trail's draw node binds it (a skin without cursortrail just gets no trail)
            hasTexture = texture != null;
            if (texture != null)
                Texture = texture;
            disjointTrail = disjoint;
            AllowPartRotation = rotate;

            if (disjoint)
            {
                TrailOrigin = centre ? Anchor.Centre : Anchor.TopLeft;
                Blending = BlendingParameters.Inherit;
            }
            else
            {
                TrailOrigin = Anchor.Centre;
                Blending = BlendingParameters.Additive;
            }
        }

        protected override double FadeDuration => disjointTrail ? 150 : 500;
        protected override float FadeExponent => 1;

        protected override bool InterpolateMovements => !disjointTrail;

        protected override float IntervalMultiplier => 1 / Math.Max(CursorSizeSetting, 1);
        protected override bool AvoidDrawingNearCursor => !disjointTrail;

        protected override void Update()
        {
            base.Update();

            if (!disjointTrail || !currentPosition.HasValue || !hasTexture)
                return;

            if (Time.Current - lastTrailTime >= disjoint_trail_time_separation)
            {
                lastTrailTime = Time.Current;
                AddTrail(currentPosition.Value);
            }
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (!hasTexture)
                return false;

            if (!disjointTrail)
                return base.OnMouseMove(e);

            currentPosition = e.ScreenSpaceMousePosition;

            // Intentionally block the base call as the trail parts are added in Update.
            return false;
        }
    }
}
