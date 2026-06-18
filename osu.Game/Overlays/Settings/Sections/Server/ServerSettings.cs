// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Server
{
    public partial class ServerSettings : SettingsSubsection
    {
        protected override LocalisableString Header => OnlineSettingsStrings.ServerConnectionHeader;

        private OsuConfigManager config = null!;
        private SettingsTextBox customApiUrlTextBox = null!;
        private SettingsTextBox customAvatarUrlTextBox = null!;

        // validate a host[:port] input without scheme or path.
        private static readonly Regex host_port_pattern = new Regex(
            @"^(?:" +
                @"(?:(?:[A-Za-z0-9-]+)\.)+[A-Za-z0-9-]+" + // multi-level domain (at least one dot)
            @"|" +
                @"(?:(?:25[0-5]|2[0-4]\d|1?\d{1,2})\.){3}(?:25[0-5]|2[0-4]\d|1?\d{1,2})" + // IPv4
            @")(?::(?<port>\d{1,5}))?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private ScheduledDelegate? pendingValidation;
        private const double debounce_delay = 500;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            this.config = config;

            Children = new Drawable[]
            {
                customApiUrlTextBox = new SettingsTextBox
                {
                    LabelText = OnlineSettingsStrings.CustomApiUrl,
                    TooltipText = OnlineSettingsStrings.CustomApiUrlTooltip,
                    Current = config.GetBindable<string>(OsuSetting.CustomApiUrl),
                    Keywords = new[] { "server", "endpoint", "api", "private", "connection" },
                },
                customAvatarUrlTextBox = new SettingsTextBox
                {
                    LabelText = OnlineSettingsStrings.CustomAvatarUrl,
                    TooltipText = OnlineSettingsStrings.CustomAvatarUrlTooltip,
                    Current = config.GetBindable<string>(OsuSetting.CustomAvatarUrl),
                    Keywords = new[] { "server", "avatar", "profile", "picture", "image" },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            customApiUrlTextBox.Current.BindValueChanged(_ => onHostUrlChanged(customApiUrlTextBox));
            customAvatarUrlTextBox.Current.BindValueChanged(_ => onHostUrlChanged(customAvatarUrlTextBox));
        }

        private bool isProgrammaticUpdate;

        private void onHostUrlChanged(SettingsTextBox textBox)
        {
            if (isProgrammaticUpdate)
                return;

            pendingValidation?.Cancel();
            pendingValidation = Scheduler.AddDelayed(() =>
            {
                string rawInput = (textBox.Current.Value ?? string.Empty).Trim();

                // empty resets to the default (which is always valid)
                if (string.IsNullOrWhiteSpace(rawInput))
                {
                    setValue(textBox, string.Empty);
                    textBox.SetNoticeText(OnlineSettingsStrings.CustomApiUrlRestartRequired, false);
                    return;
                }

                string hostPort = stripSchemeAndPath(rawInput);

                if (!isValidHostPort(hostPort))
                {
                    textBox.SetNoticeText(OnlineSettingsStrings.CustomApiUrlInvalid, true);
                    return;
                }

                // stored value as bare host[:port].
                if (!string.Equals(rawInput, hostPort, StringComparison.Ordinal))
                    setValue(textBox, hostPort);

                textBox.SetNoticeText(OnlineSettingsStrings.CustomApiUrlRestartRequired, false);
            }, debounce_delay);
        }

        private void setValue(SettingsTextBox textBox, string value)
        {
            isProgrammaticUpdate = true;
            textBox.Current.Value = value;
            isProgrammaticUpdate = false;

            // FORCE FLUSH to disk so the value survives even if the client is closed
            // before the bindable's debounced background save runs.
            config.Save();
        }

        private static bool isValidHostPort(string hostPort)
        {
            var m = host_port_pattern.Match(hostPort);
            if (!m.Success)
                return false;

            var g = m.Groups["port"];
            if (g.Success)
                return int.TryParse(g.Value, out int port) && port >= 1 && port <= 65535;

            return true;
        }

        /// <summary>
        /// Strips any path from endpoint input leaving a bare host[:port].
        /// </summary>
        private static string stripSchemeAndPath(string input)
        {
            string s = Regex.Replace(input, @"^\s*https?://", string.Empty, RegexOptions.IgnoreCase);

            int slash = s.IndexOf('/');
            if (slash >= 0)
                s = s.Substring(0, slash);

            return s.TrimEnd('/');
        }
    }
}
