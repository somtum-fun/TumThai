// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModDroid : Mod, IApplicableToBeatmap, IApplicableToDrawableRuleset<OsuHitObject>, IApplicableToPlayer
    {
        /// <summary>
        /// Offset used by osu!droid (<c>CircleSizeCalculator.DROID_STANDARD_CS_OFFSET</c>) to convert osu!standard CS into osu!droid scale.
        /// </summary>
        public const float DROID_STANDARD_CS_OFFSET = 6.855634f;

        /// <summary>
        /// Stack offset multiplier used by osu!droid (<c>-4f</c> vs osu!standard's <c>-6.4f</c>).
        /// </summary>
        public const float DROID_STACK_OFFSET_MULTIPLIER = -4f;

        /// <summary>
        /// Expanded slider follow circle multiplier used by osu!droid (<c>2.0f</c> vs osu!lazer's <c>2.4f</c>).
        /// </summary>
        public const float DROID_FOLLOW_AREA = 2.0f;

        public override string Name => "osu!droid";
        public override string Acronym => "DR";
        public override IconUsage? Icon => FontAwesome.Brands.Android;
        public override ModType Type => ModType.Conversion;
        public override LocalisableString Description => "osu!droid circle size, hit windows, and 3-cursor multi-touch controls.";
        public override string ExtendedIconInformation => PreciseTiming.Value ? "PR" : string.Empty;

        public override bool UserPlayable => RuntimeInfo.IsMobile;
        public override bool Ranked => false;

        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModAutoplay),
            typeof(OsuModAutopilot),
            typeof(ModTouchDevice),
            typeof(OsuModClassic),
            typeof(OsuModStrictTracking),
            typeof(InputBlockingMod),
            typeof(OsuModTargetPractice),
            typeof(OsuModBloom),
        };

        [SettingSource("Precise timing", "Use osu!droid Precise (PR) hit windows.")]
        public BindableBool PreciseTiming { get; } = new BindableBool();

        private OsuInputManager osuInputManager = null!;

        /// <summary>
        /// Converts a difficulty CS value into osu!droid object scale, matching <c>CircleSizeCalculator.droidCSToDroidScale</c>.
        /// </summary>
        public static float CalculateDroidScale(float circleSize)
            => Math.Max(1e-3f, LegacyRulesetExtensions.CalculateScaleFromCircleSize(circleSize - DROID_STANDARD_CS_OFFSET, true));

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            float droidScale = CalculateDroidScale(beatmap.Difficulty.CircleSize);
            double overallDifficulty = beatmap.Difficulty.OverallDifficulty;
            bool precise = PreciseTiming.Value;

            foreach (var hitObject in beatmap.HitObjects)
            {
                switch (hitObject)
                {
                    case HitCircle circle:
                        circle.HitWindows = new OsuDroidHitWindows(precise);
                        circle.HitWindows.SetDifficulty(overallDifficulty);
                        break;

                    case Slider slider:
                        slider.ClassicSliderBehaviour = true;
                        slider.FollowCircleArea = DROID_FOLLOW_AREA;
                        slider.RequireInitialActionForTracking = false;

                        if (slider.HeadCircle != null)
                        {
                            slider.HeadCircle.HitWindows = new OsuDroidHitWindows(precise);
                            slider.HeadCircle.HitWindows.SetDifficulty(overallDifficulty);
                        }

                        break;

                    case Spinner spinner:
                        spinner.UseDroidSpinRequirement = true;
                        spinner.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                        break;
                }

                if (hitObject is OsuHitObject osuObject)
                    applyDroidScaleRecursive(osuObject, droidScale);
            }
        }

        private static void applyDroidScaleRecursive(OsuHitObject hitObject, float droidScale)
        {
            hitObject.Scale = droidScale;
            hitObject.StackOffsetMultiplier = DROID_STACK_OFFSET_MULTIPLIER;

            foreach (HitObject nested in hitObject.NestedHitObjects)
            {
                if (nested is OsuHitObject nestedOsu)
                    applyDroidScaleRecursive(nestedOsu, droidScale);
            }
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            var osuRuleset = (DrawableOsuRuleset)drawableRuleset;
            osuRuleset.Playfield.ApplyCircleSizeScaleToPlayfieldBorder(CalculateDroidScale(osuRuleset.Beatmap.Difficulty.CircleSize));

            osuInputManager = osuRuleset.KeyBindingInputManager;
            osuInputManager.SetTouchInputMapper(new OsuDroidTouchInputMapper(osuInputManager, osuRuleset.Playfield)
            {
                RelativeSizeAxes = Axes.Both,
            });
        }

        public void ApplyToPlayer(Player player)
        {
            if (osuInputManager.ReplayInputHandler != null)
                return;

            osuInputManager.AllowNonTouchGameplayInputs = false;
        }
    }
}
