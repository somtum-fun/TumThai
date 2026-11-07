// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterfaceV2
{
    public partial class FormCheckBox : CompositeDrawable, IHasCurrentValue<bool>, IFormControl
    {
        public Bindable<bool> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        /// <summary>
        /// Whether this checkbox should be extra tall to match up with other form controls in vertical sizing scenarios.
        /// </summary>
        public bool ExtendedHeight { get; init; }

        private readonly BindableWithCurrent<bool> current = new BindableWithCurrent<bool>();

        /// <summary>
        /// Caption describing this control, displayed on top of the controls.
        /// </summary>
        public LocalisableString Caption { get; init; }

        /// <summary>
        /// Hint text containing an extended description of this control, displayed in a tooltip when hovering the caption.
        /// </summary>
        public LocalisableString HintText { get; init; }

        private FormControlBackground background = null!;
        private Container rowContainer = null!;
        private Container captionContainer = null!;
        private FormFieldCaption caption = null!;

        private SwitchButton switchButton = null!;
        private IBindable<InterfaceTheme>? interfaceTheme;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager? config)
        {
            interfaceTheme = config?.GetBindable<InterfaceTheme>(OsuSetting.InterfaceTheme);

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChildren = new Drawable[]
            {
                background = new FormControlBackground(),
                rowContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = ExtendedHeight ? 52 : 0,
                    AutoSizeAxes = ExtendedHeight ? Axes.None : Axes.Y,
                    Padding = new MarginPadding(9),
                    Children = new Drawable[]
                    {
                        captionContainer = new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding { Right = SwitchButton.WIDTH + 5 },
                            Children = new Drawable[]
                            {
                                caption = new FormFieldCaption
                                {
                                    Caption = Caption,
                                    TooltipText = HintText,
                                },
                            },
                        },
                        switchButton = new SwitchButton
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Current = Current,
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            interfaceTheme?.BindValueChanged(_ => updateState(), true);

            current.BindValueChanged(_ =>
            {
                updateState();
                if (interfaceTheme?.Value.IsThemed() != true)
                    background.FlashOnCommit();

                ValueChanged?.Invoke();
            });
            current.BindDisabledChanged(_ => updateState(), true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            updateState();
        }

        protected override bool OnClick(ClickEvent e)
        {
            switchButton.TriggerClick();
            return true;
        }

        private void updateState()
        {
            bool isThemed = interfaceTheme?.Value.IsThemed() == true;

            background.Alpha = isThemed ? 0 : 1;
            if (isThemed)
            {
                rowContainer.Padding = new MarginPadding { Vertical = 4, Horizontal = 2 };
                switchButton.Anchor = Anchor.CentreLeft;
                switchButton.Origin = Anchor.CentreLeft;
                captionContainer.Padding = new MarginPadding { Left = 26 };
                caption.Colour = Current.Disabled ? colourProvider.Background1 : Color4.White;
                return;
            }

            rowContainer.Padding = new MarginPadding(9);
            switchButton.Anchor = Anchor.CentreRight;
            switchButton.Origin = Anchor.CentreRight;
            captionContainer.Padding = new MarginPadding { Right = SwitchButton.WIDTH + 5 };
            caption.Colour = Current.Disabled ? colourProvider.Background1 : colourProvider.Content2;

            if (IsDisabled)
                background.VisualStyle = VisualStyle.Disabled;
            else if (IsHovered)
                background.VisualStyle = VisualStyle.Hovered;
            else
                background.VisualStyle = VisualStyle.Normal;
        }

        public IEnumerable<LocalisableString> FilterTerms => new[] { Caption, HintText };

        public event Action? ValueChanged;

        public bool IsDefault => Current.IsDefault;

        public void SetDefault() => Current.SetDefault();

        public bool IsDisabled => Current.Disabled;

        public float MainDrawHeight => DrawHeight;
    }
}
