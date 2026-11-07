// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Graphics.Cursor
{
    public partial class MenuCursorContainer : CursorContainer
    {
        private readonly IBindable<bool> screenshotCursorVisibility = new Bindable<bool>(true);
        public override bool IsPresent => screenshotCursorVisibility.Value && base.IsPresent;

        private bool hideCursorOnNonMouseInput;

        public bool HideCursorOnNonMouseInput
        {
            get => hideCursorOnNonMouseInput;
            set
            {
                if (hideCursorOnNonMouseInput == value)
                    return;

                hideCursorOnNonMouseInput = value;
                updateState();
            }
        }

        protected override Drawable CreateCursor()
        {
            activeCursor = new Cursor();
            activeCursor.StableTrailChanged += (texture, disjoint, centre, rotate) =>
            {
                pendingTrail = (texture, disjoint, centre, rotate);

                // the cursor can report before this container's load has made the trail; it's applied there then
                if (stableTrail != null)
                    stableTrail.Apply(texture, disjoint, centre, rotate);
            };
            return activeCursor;
        }

        private Cursor activeCursor = null!;

        private DragRotationState dragRotationState;
        private Vector2 positionMouseDown;
        private Vector2 lastMovePosition;

        private Bindable<bool> cursorRotate = null!;
        private Sample tapSample = null!;

        private MouseInputDetector mouseInputDetector = null!;

        private bool visible;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, ScreenshotManager? screenshotManager, AudioManager audio)
        {
            cursorRotate = config.GetBindable<bool>(OsuSetting.CursorRotation);

            if (screenshotManager != null)
                screenshotCursorVisibility.BindTo(screenshotManager.CursorVisibility);

            tapSample = audio.Samples.Get(@"UI/cursor-tap");

            Add(mouseInputDetector = new MouseInputDetector());

            // the osu!stable theme's trail, behind the cursor (the same trail as gameplay)
            Add(stableTrail = new StableMenuCursorTrail { Depth = 1, Alpha = 0 });
            gameplayCursorSize = config.GetBindable<float>(OsuSetting.GameplayCursorSize);

            if (pendingTrail is var (texture, disjoint, centre, rotate))
                stableTrail.Apply(texture, disjoint, centre, rotate);
        }

        private (Texture? texture, bool disjoint, bool centre, bool rotate)? pendingTrail;
        private StableMenuCursorTrail? stableTrail;
        private Bindable<float> gameplayCursorSize = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        private readonly IBindable<bool> lastInputWasMouse = new BindableBool();
        private readonly IBindable<bool> gameActive = new BindableBool(true);
        private readonly IBindable<bool> gameIdle = new BindableBool();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            lastInputWasMouse.BindTo(mouseInputDetector.LastInputWasMouseSource);
            lastInputWasMouse.BindValueChanged(_ => updateState(), true);

            if (game != null)
            {
                gameIdle.BindTo(game.IsIdle);
                gameIdle.BindValueChanged(_ => updateState());

                gameActive.BindTo(game.IsActive);
                gameActive.BindValueChanged(_ => updateState());
            }
        }

        protected override void UpdateState(ValueChangedEvent<Visibility> state) => updateState();

        private void updateState()
        {
            bool combinedVisibility = getCursorVisibility();

            if (visible == combinedVisibility)
                return;

            visible = combinedVisibility;

            if (visible)
                PopIn();
            else
                PopOut();
        }

        private bool getCursorVisibility()
        {
            // do not display when explicitly set to hidden state.
            if (State.Value == Visibility.Hidden)
                return false;

            // only hide cursor when game is focused, otherwise it should always be displayed.
            if (gameActive.Value)
            {
                // do not display when last input is not mouse.
                if (hideCursorOnNonMouseInput && !lastInputWasMouse.Value)
                    return false;

                // do not display when game is idle.
                if (gameIdle.Value)
                    return false;
            }

            return true;
        }

        protected override void Update()
        {
            base.Update();

            if (stableTrail != null)
            {
                bool stable = activeCursor.UsingStableCursor;
                stableTrail.Alpha = stable ? activeCursor.Alpha : 0;

                if (stable)
                {
                    stableTrail.CursorScale = activeCursor.StableScale;
                    stableTrail.PartRotation = activeCursor.StableRotation;
                    stableTrail.CursorSizeSetting = gameplayCursorSize.Value;
                }
            }

            if (dragRotationState != DragRotationState.NotDragging
                && Vector2.Distance(positionMouseDown, lastMovePosition) > 60)
            {
                // make the rotation centre point floating.
                positionMouseDown = Interpolation.ValueAt(0.04f, positionMouseDown, lastMovePosition, 0, Clock.ElapsedFrameTime);
            }
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (dragRotationState != DragRotationState.NotDragging)
            {
                lastMovePosition = e.MousePosition;

                float distance = Vector2Extensions.Distance(lastMovePosition, positionMouseDown);

                // don't start rotating until we're moved a minimum distance away from the mouse down location,
                // else it can have an annoying effect.
                if (dragRotationState == DragRotationState.DragStarted && distance > 80)
                    dragRotationState = DragRotationState.Rotating;

                // don't rotate when distance is zero to avoid NaN
                if (dragRotationState == DragRotationState.Rotating && distance > 0)
                {
                    Vector2 offset = e.MousePosition - positionMouseDown;
                    float degrees = float.RadiansToDegrees(MathF.Atan2(-offset.X, offset.Y)) + 24.3f;

                    // Always rotate in the direction of least distance
                    float diff = (degrees - activeCursor.Rotation) % 360;
                    if (diff < -180) diff += 360;
                    if (diff > 180) diff -= 360;
                    degrees = activeCursor.Rotation + diff;

                    activeCursor.RotateTo(degrees, 120, Easing.OutQuint);
                }
            }

            return base.OnMouseMove(e);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (State.Value == Visibility.Visible && activeCursor.UsingStableCursor)
            {
                activeCursor.StableExpand();
                playTapSample();
            }
            else if (State.Value == Visibility.Visible)
            {
                // only trigger animation for main mouse buttons
                activeCursor.Scale = new Vector2(1);
                activeCursor.ScaleTo(0.90f, 800, Easing.OutQuint);

                activeCursor.AdditiveLayer.Alpha = 0;
                activeCursor.AdditiveLayer.FadeInFromZero(800, Easing.OutQuint);

                if (cursorRotate.Value && dragRotationState != DragRotationState.Rotating)
                {
                    // if cursor is already rotating don't reset its rotate origin
                    dragRotationState = DragRotationState.DragStarted;
                    positionMouseDown = e.MousePosition;
                }

                playTapSample();
            }

            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (!e.HasAnyButtonPressed && activeCursor.UsingStableCursor)
            {
                activeCursor.StableContract();
            }
            else if (!e.HasAnyButtonPressed)
            {
                activeCursor.AdditiveLayer.FadeOutFromOne(500, Easing.OutQuint);
                activeCursor.ScaleTo(1, 500, Easing.OutElastic);

                if (dragRotationState != DragRotationState.NotDragging)
                {
                    activeCursor.RotateTo(0, 400 * (0.5f + Math.Abs(activeCursor.Rotation / 960)), Easing.OutElasticQuarter);
                    dragRotationState = DragRotationState.NotDragging;
                }

                if (State.Value == Visibility.Visible)
                    playTapSample(0.8);
            }

            base.OnMouseUp(e);
        }

        protected override void PopIn()
        {
            activeCursor.FadeTo(1, 250, Easing.OutQuint);
            activeCursor.ScaleTo(1, 400, Easing.OutQuint);

            if (dragRotationState == DragRotationState.NotDragging)
                activeCursor.RotateTo(0, 400, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            activeCursor.FadeTo(0, 250, Easing.OutQuint);
            activeCursor.ScaleTo(0.6f, 250, Easing.In);

            if (dragRotationState == DragRotationState.NotDragging)
                activeCursor.RotateTo(0, 400, Easing.OutQuint);
        }

        private void playTapSample(double baseFrequency = 1f)
        {
            const float random_range = 0.02f;
            SampleChannel channel = tapSample.GetChannel();

            // Scale to [-0.75, 0.75] so that the sample isn't fully panned left or right (sounds weird)
            channel.Balance.Value = ((activeCursor.X / DrawWidth) * 2 - 1) * OsuGameBase.SFX_STEREO_STRENGTH;
            channel.Frequency.Value = baseFrequency - (random_range / 2f) + RNG.NextDouble(random_range);
            channel.Volume.Value = baseFrequency;

            channel.Play();
        }

        public partial class Cursor : Container
        {
            private Container cursorContainer = null!;
            private Bindable<float> cursorScale = null!;
            private const float base_scale = 0.15f;

            public Sprite AdditiveLayer = null!;

            // TumThai!Next: in the osu!stable theme, the skin's own cursor (cursor + cursormiddle), as osu!stable draws it.
            private Container stableCursor = null!;
            private Sprite stableCursorSprite = null!;
            private Sprite stableCursorMiddleSprite = null!;
            private Bindable<InterfaceTheme> interfaceTheme = null!;
            private Bindable<float> gameplayCursorScale = null!;
            private IBindable<Skin>? currentSkin;

            // the gameplay cursor's spin (LegacyCursor.REVOLUTION_DURATION)
            private const double stable_revolution_duration = 10000;

            [Resolved]
            private SkinManager? skinManager { get; set; }

            public Cursor()
            {
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config, TextureStore textures, OsuColour colour)
            {
                Children = new Drawable[]
                {
                    cursorContainer = new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new Sprite
                            {
                                Texture = textures.Get(@"Cursor/menu-cursor"),
                            },
                            AdditiveLayer = new Sprite
                            {
                                Blending = BlendingParameters.Additive,
                                Colour = colour.Pink,
                                Alpha = 0,
                                Texture = textures.Get(@"Cursor/menu-cursor-additive"),
                            },
                        }
                    },
                    stableCursor = new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            stableCursorSprite = new Sprite(),
                            stableCursorMiddleSprite = new Sprite(),
                        }
                    },
                };

                cursorScale = config.GetBindable<float>(OsuSetting.MenuCursorSize);
                cursorScale.BindValueChanged(scale => cursorContainer.Scale = new Vector2(scale.NewValue * base_scale), true);

                // the stable theme's cursor is the gameplay one, at the gameplay cursor size
                gameplayCursorScale = config.GetBindable<float>(OsuSetting.GameplayCursorSize);

                interfaceTheme = config.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (skinManager != null)
                {
                    currentSkin = skinManager.CurrentSkin.GetBoundCopy();
                    currentSkin.BindValueChanged(_ => updateStableCursor());
                }

                interfaceTheme.BindValueChanged(_ => updateStableCursor(), true);
            }

            /// <summary>
            /// Whether the osu!stable theme's cursor (the skin's gameplay cursor) is showing instead of lazer's.
            /// </summary>
            public bool UsingStableCursor => stableCursor.Alpha > 0;

            /// <summary>
            /// The stable cursor's scale in this cursor's parent's units (the trail uses it to match the cursor's size).
            /// </summary>
            public Vector2 StableScale => stableCursor.Scale;

            /// <summary>
            /// The stable cursor's spin, which the trail's parts follow (as in gameplay).
            /// </summary>
            public float StableRotation => stableCursorSprite.Rotation;

            /// <summary>
            /// Raised with the trail's set-up whenever the stable cursor changes (theme or skin).
            /// </summary>
            public event Action<Texture?, bool, bool, bool>? StableTrailChanged;

            private bool expand = true;

            private static bool skinFlag(Skin? skin, string key, bool fallback)
            {
                if (skin is not LegacySkin legacy || !legacy.Configuration.ConfigDictionary.TryGetValue(key, out string? value))
                    return fallback;

                return value.Trim() != "0";
            }

            private void updateStableCursor()
            {
                if (!interfaceTheme.Value.IsThemed() || skinManager == null)
                {
                    stableCursor.Alpha = 0;
                    cursorContainer.Alpha = 1;
                    return;
                }

                // As gameplay: the cursor and its middle come from the same skin, the first that has a cursor (the player's,
                // else osu!stable's default). A skin with a cursor but no cursormiddle shows none (and a disjoint trail).
                Skin? userSkin = skinManager.CurrentSkin.Value;
                ISkin provider = userSkin is LegacySkin && userSkin != skinManager.DefaultClassicSkin && userSkin.GetTexture(@"cursor") != null
                    ? userSkin
                    : skinManager.DefaultClassicSkin;

                var cursorTexture = provider.GetTexture(@"cursor");

                if (cursorTexture == null)
                {
                    stableCursor.Alpha = 0;
                    cursorContainer.Alpha = 1;
                    return;
                }

                var middleTexture = provider.GetTexture(@"cursormiddle");
                stableCursorSprite.Texture = cursorTexture;
                stableCursorMiddleSprite.Texture = middleTexture;
                stableCursorMiddleSprite.Alpha = middleTexture != null ? 1 : 0;

                Skin? configSkin = provider as Skin;
                bool centre = skinFlag(configSkin, @"CursorCentre", true);
                bool rotate = skinFlag(configSkin, @"CursorRotate", true);
                expand = skinFlag(configSkin, @"CursorExpand", true);

                var origin = centre ? Anchor.Centre : Anchor.TopLeft;
                stableCursorSprite.Origin = stableCursorMiddleSprite.Origin = origin;
                stableCursor.Origin = Anchor.TopLeft;

                stableCursorSprite.ClearTransforms();
                stableCursorSprite.Rotation = 0;
                stableCursorSprite.Scale = Vector2.One;
                if (rotate)
                    stableCursorSprite.Spin(stable_revolution_duration, RotationDirection.Clockwise);

                stableCursor.Alpha = 1;
                cursorContainer.Alpha = 0;

                // the trail: the player's cursortrail, else the default skin's (as gameplay's skin lookup)
                var trailTexture = (userSkin is LegacySkin ? userSkin.GetTexture(@"cursortrail") : null)
                                   ?? skinManager.DefaultClassicSkin.GetTexture(@"cursortrail");

                StableTrailChanged?.Invoke(trailTexture, middleTexture == null, centre, skinFlag(userSkin, @"CursorTrailRotate", true));
            }

            /// <summary>
            /// Gameplay's press: the cursor (not its middle) grows to 1.3 (LegacyCursor), unless the skin turns CursorExpand off.
            /// </summary>
            public void StableExpand()
            {
                if (expand)
                    stableCursorSprite.ScaleTo(1f).ScaleTo(1.3f, 100, Easing.Out);
            }

            public void StableContract() => stableCursorSprite.ScaleTo(1f, 100, Easing.Out);

            protected override void Update()
            {
                base.Update();

                if (stableCursor.Alpha == 0 || Parent == null)
                    return;

                // Sized as osu!stable (and the gameplay cursor) draws it: skin pixels in a 480-line-high screen. The menu
                // cursor lives outside the UI's scaling, so work it out from the window's height in this cursor's units.
                Drawable root = this;
                while (root.Parent != null)
                    root = root.Parent;

                float pixelsPerUnit = Parent.ScreenSpaceDrawQuad.Height / Math.Max(1, Parent.DrawHeight);
                float windowHeight = root.ScreenSpaceDrawQuad.Height;

                if (pixelsPerUnit > 0 && windowHeight > 0)
                    stableCursor.Scale = new Vector2(windowHeight / 480f / pixelsPerUnit * gameplayCursorScale.Value);
            }
        }

        private partial class MouseInputDetector : Component
        {
            /// <summary>
            /// Whether the last input applied to the game is sourced from mouse.
            /// </summary>
            public IBindable<bool> LastInputWasMouseSource => lastInputWasMouseSource;

            private readonly Bindable<bool> lastInputWasMouseSource = new Bindable<bool>();

            public MouseInputDetector()
            {
                RelativeSizeAxes = Axes.Both;
            }

            protected override bool Handle(UIEvent e)
            {
                switch (e)
                {
                    case MouseDownEvent:
                    case MouseMoveEvent:
                        lastInputWasMouseSource.Value = true;
                        return false;

                    case KeyDownEvent keyDown when !keyDown.Repeat:
                    case JoystickPressEvent:
                    case MidiDownEvent:
                        lastInputWasMouseSource.Value = false;
                        return false;
                }

                return false;
            }
        }

        private enum DragRotationState
        {
            NotDragging,
            DragStarted,
            Rotating,
        }
    }
}
