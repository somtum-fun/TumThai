// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Screens.Footer;

namespace osu.Game.Graphics.UserInterface
{
    // todo: remove this once all screens migrate to display the new game footer and back button.
    public partial class BackButton : VisibilityContainer
    {
        public Action? Action { get; init; }

        private readonly TwoLayerButton button;
        private readonly StableBackButton stableButton;
        private readonly Bindable<InterfaceTheme> interfaceTheme = new Bindable<InterfaceTheme>();

        public BackButton(ScreenFooter.BackReceptor? receptor = null)
        {
            Size = TwoLayerButton.SIZE_EXTENDED;

            Children = new Drawable[]
            {
                button = new TwoLayerButton
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Text = CommonStrings.Back.ToLower(),
                    Icon = OsuIcon.LeftCircle,
                    Action = () => Action?.Invoke()
                },
                stableButton = new StableBackButton
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Alpha = 0,
                    Action = () => Action?.Invoke()
                }
            };

            if (receptor == null)
            {
                // if a receptor wasn't provided, create our own locally.
                Add(receptor = new ScreenFooter.BackReceptor());
            }

            receptor.OnBackPressed = () => button.TriggerClick();
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, OsuConfigManager? config)
        {
            button.BackgroundColour = colours.Pink;
            button.HoverColour = colours.PinkDark;

            config?.BindWith(OsuSetting.InterfaceTheme, interfaceTheme);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            interfaceTheme.BindValueChanged(_ => updateThemeVisibility(), true);
        }

        private void updateThemeVisibility()
        {
            bool useStable = interfaceTheme.Value.IsThemed();
            button.Alpha = useStable ? 0 : 1;
            stableButton.Alpha = useStable ? 1 : 0;
        }

        protected override void PopIn()
        {
            this.MoveToX(0, 400, Easing.OutQuint);
            this.FadeIn(150, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            this.MoveToX(-TwoLayerButton.SIZE_EXTENDED.X / 2, 400, Easing.OutQuint);
            this.FadeOut(400, Easing.OutQuint);
        }
    }
}
