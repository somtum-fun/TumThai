// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Audio
{
    public partial class VolumeSettings : SettingsSubsection
    {
        protected override LocalisableString Header => AudioSettingsStrings.VolumeHeader;

        private Bindable<bool> inactiveEnabled = null!;
        private SettingsItemV2 inactiveVolume = null!;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, OsuConfigManager config)
        {
            inactiveEnabled = config.GetBindable<bool>(OsuSetting.VolumeInactiveEnabled);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = AudioSettingsStrings.MasterVolume,
                    Current = audio.Volume,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                    PlaySamplesOnAdjust = false,
                }),
                // TumThai: whether the volume goes down at all while the game isn't focused.
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = AudioSettingsStrings.LowerVolumeWhenInactive,
                    HintText = AudioSettingsStrings.LowerVolumeWhenInactiveTooltip,
                    Current = inactiveEnabled,
                }),
                inactiveVolume = new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = AudioSettingsStrings.MasterVolumeInactive,
                    Current = config.GetBindable<double>(OsuSetting.VolumeInactive),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = AudioSettingsStrings.MusicVolume,
                    Current = audio.VolumeTrack,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                    PlaySamplesOnAdjust = false,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = AudioSettingsStrings.EffectVolume,
                    Current = audio.VolumeSample,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                    PlaySamplesOnAdjust = false,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = AudioSettingsStrings.HitsoundVolume,
                    Current = config.GetBindable<double>(OsuSetting.HitsoundVolume),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                    PlaySamplesOnAdjust = false,
                }),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // TumThai: the inactive level only matters while lowering the volume is on.
            inactiveEnabled.BindValueChanged(enabled => inactiveVolume.Alpha = enabled.NewValue ? 1 : 0, true);
        }
    }
}
