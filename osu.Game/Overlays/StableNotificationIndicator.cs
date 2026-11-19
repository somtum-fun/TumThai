// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Performance;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    /// <summary>
    /// TumThai!Next: the osu!stable theme hides lazer's toolbar, and with it the way to notifications (a download's progress
    /// and its cancel). Like osu!stable's download popup, this small bell in the top-right corner shows up only when there's
    /// something to see: it spins while something is going on (a download, an import) and counts unread notifications.
    /// A tap or click opens notifications. The main menu has its own "Notifications" tab, so it's not shown there; nor
    /// in gameplay, while toasts are showing, or while notifications are open.
    /// </summary>
    public partial class StableNotificationIndicator : OsuClickableContainer
    {
        private const float size = 34;

        private readonly NotificationOverlay notifications;
        private readonly IBindable<OverlayActivation> overlayActivationMode;
        private readonly Func<bool> atMainMenu;

        private Bindable<InterfaceTheme> interfaceTheme = null!;
        private IBindable<int> unreadCount = null!;

        private Container body = null!;
        private Container badge = null!;
        private OsuSpriteText badgeText = null!;
        private SpriteIcon busyRing = null!;

        private bool busy;
        private bool shown;

        public StableNotificationIndicator(NotificationOverlay notifications, IBindable<OverlayActivation> overlayActivationMode, Func<bool> atMainMenu)
        {
            this.notifications = notifications;
            this.overlayActivationMode = overlayActivationMode.GetBoundCopy();
            this.atMainMenu = atMainMenu;

            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;
            Margin = new MarginPadding(6);
            Size = new Vector2(size);
            Alpha = 0;
            AlwaysPresent = true;

            Action = () => notifications.ToggleVisibility();
            TooltipText = NotificationsStrings.HeaderTitle;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, OsuColour colours)
        {
            interfaceTheme = config.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);

            Children = new Drawable[]
            {
                body = new CircularContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    BorderThickness = 2,
                    BorderColour = Color4.White.Opacity(0.5f),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.6f),
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(14),
                            Icon = FontAwesome.Solid.Bell,
                        },
                    }
                },
                busyRing = new SpriteIcon
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = FontAwesome.Solid.CircleNotch,
                    Colour = colours.Yellow,
                    Alpha = 0,
                },
                badge = new CircularContainer
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-4, 4),
                    AutoSizeAxes = Axes.X,
                    Height = 16,
                    Masking = true,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4Extensions.FromHex(@"ff66aa"),
                        },
                        badgeText = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Margin = new MarginPadding { Horizontal = 6 },
                            Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                        },
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            unreadCount = notifications.UnreadCount.GetBoundCopy();
            unreadCount.BindValueChanged(count =>
            {
                badgeText.Text = count.NewValue > 99 ? "99+" : count.NewValue.ToString();
                badge.Alpha = count.NewValue > 0 ? 1 : 0;
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            setBusy(((INotificationOverlay)notifications).HasOngoingOperations);

            bool show = interfaceTheme.Value.IsThemed()
                        && overlayActivationMode.Value != OverlayActivation.Disabled
                        && notifications.State.Value != Visibility.Visible
                        && notifications.ToastCount == 0
                        && (busy || unreadCount.Value > 0)
                        && !atMainMenu();

            if (show == shown)
                return;

            shown = show;
            double duration = PotatoMode.UltraActive ? 0 : 200;

            if (show)
                this.FadeIn(duration, Easing.OutQuint);
            else
                this.FadeOut(duration, Easing.OutQuint);
        }

        private void setBusy(bool value)
        {
            if (busy == value)
                return;

            busy = value;
            busyRing.ClearTransforms();
            busyRing.Rotation = 0;
            busyRing.Alpha = busy ? 1 : 0;

            if (busy && !PotatoMode.UltraActive)
                busyRing.Spin(1000, RotationDirection.Clockwise);
        }

        // Hidden, it takes no input: the corner stays the screen's.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => shown && base.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnHover(HoverEvent e)
        {
            body.BorderColour = Color4.White;
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            body.BorderColour = Color4.White.Opacity(0.5f);
            base.OnHoverLost(e);
        }
    }
}
