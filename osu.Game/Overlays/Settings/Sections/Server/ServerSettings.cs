// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.Server
{
    public partial class ServerSettings : SettingsSubsection
    {
        protected override LocalisableString Header => OnlineSettingsStrings.ServerConnectionHeader;

        private const string default_profile_option = "Default (somtum.fun)";
        private const string new_profile_option = "+ Add New Private Server Profile";

        // validate a host[:port] input without scheme or path.
        private static readonly Regex host_port_pattern = new Regex(
            @"^(?:" +
                @"(?:(?:[A-Za-z0-9-]+)\.)+[A-Za-z0-9-]+" + // multi-level domain (at least one dot)
            @"|" +
                @"(?:(?:25[0-5]|2[0-4]\d|1?\d{1,2})\.){3}(?:25[0-5]|2[0-4]\d|1?\d{1,2})" + // IPv4
            @")(?::(?<port>\d{1,5}))?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private OsuConfigManager config = null!;

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        private string activeProfileId => config.Get<string>(OsuSetting.ActiveExternalServerProfileId);

        private Bindable<bool> externalServerEnabledConfig = null!;
        private readonly BindableBool enableToggleBindable = new BindableBool();
        private bool isUpdatingToggleProgrammatically;

        private FillFlowContainer detailsContainer = null!;
        private SettingsDropdown<string> profileDropdown = null!;
        private SettingsTextBox profileNameTextBox = null!;
        private SettingsTextBox customApiUrlTextBox = null!;
        private SettingsTextBox customWebsiteUrlTextBox = null!;
        private SettingsTextBox customAvatarUrlTextBox = null!;
        private SettingsTextBox customClientIdTextBox = null!;
        private SettingsTextBox customClientSecretTextBox = null!;
        private SettingsButtonV2 saveProfileButton = null!;
        private DangerousSettingsButtonV2 deleteProfileButton = null!;

        private List<ExternalServerProfile> savedProfiles = new List<ExternalServerProfile>();
        private string? editingProfileId;
        private bool isUpdatingDropdownProgrammatically;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            this.config = config;
            externalServerEnabledConfig = config.GetBindable<bool>(OsuSetting.ExternalServerEnabled);

            Children = new Drawable[]
            {
                new SettingsCheckbox
                {
                    LabelText = "Enable connecting external server (non-somtum.fun)",
                    TooltipText = "TumThai is optimized for somtum.fun only. Playing on another private server may be broken!",
                    Current = enableToggleBindable,
                    Keywords = new[] { "server", "external", "private", "custom", "somtum", "advanced" },
                },
                detailsContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new osuTK.Vector2(0, 8),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        profileDropdown = new SettingsDropdown<string>
                        {
                            LabelText = "Server profile",
                            TooltipText = "Select a saved private server profile or create a new one.",
                            ShowsDefaultIndicator = false,
                            Keywords = new[] { "profile", "server", "private" },
                        },
                        profileNameTextBox = new SettingsTextBox
                        {
                            LabelText = "Profile name",
                            TooltipText = "Display name for this private server profile.",
                            Current = new Bindable<string>(string.Empty),
                            ShowsDefaultIndicator = false,
                            Keywords = new[] { "profile", "name", "server" },
                        },
                        customApiUrlTextBox = new SettingsTextBox
                        {
                            LabelText = OnlineSettingsStrings.CustomApiUrl,
                            TooltipText = "Host of the private server API (e.g. \"osu.example.com\" or \"dev.ppy.sh\"). *.ppy.sh (except dev.ppy.sh) is blocked.",
                            Current = new Bindable<string>(string.Empty),
                            ShowsDefaultIndicator = false,
                            Keywords = new[] { "server", "endpoint", "api", "private", "connection" },
                        },
                        customWebsiteUrlTextBox = new SettingsTextBox
                        {
                            LabelText = "Custom website URL (optional)",
                            TooltipText = "Host of the private server website (leave empty to derive automatically from API URL).",
                            Current = new Bindable<string>(string.Empty),
                            ShowsDefaultIndicator = false,
                            Keywords = new[] { "server", "website", "url" },
                        },
                        customAvatarUrlTextBox = new SettingsTextBox
                        {
                            LabelText = OnlineSettingsStrings.CustomAvatarUrl,
                            TooltipText = OnlineSettingsStrings.CustomAvatarUrlTooltip,
                            Current = new Bindable<string>(string.Empty),
                            ShowsDefaultIndicator = false,
                            Keywords = new[] { "server", "avatar", "profile", "picture", "image" },
                        },
                        customClientIdTextBox = new SettingsTextBox
                        {
                            LabelText = "OAuth Client ID (optional)",
                            TooltipText = "OAuth client_id for the private server (leave empty to use default \"5\").",
                            Current = new Bindable<string>(string.Empty),
                            ShowsDefaultIndicator = false,
                            Keywords = new[] { "client", "id", "oauth", "token" },
                        },
                        customClientSecretTextBox = new SettingsTextBox
                        {
                            LabelText = "OAuth Client Secret (optional)",
                            TooltipText = "OAuth client_secret for the private server (leave empty to use default).",
                            Current = new Bindable<string>(string.Empty),
                            ShowsDefaultIndicator = false,
                            Keywords = new[] { "client", "secret", "oauth", "token" },
                        },
                        saveProfileButton = new SettingsButtonV2
                        {
                            Text = "Save & Activate Profile",
                            TooltipText = "Save this private server profile and set it as the active server (restart required).",
                            Action = onSaveAndActivateClicked,
                            Keywords = new[] { "save", "profile", "server", "apply" },
                        },
                        deleteProfileButton = new DangerousSettingsButtonV2
                        {
                            Text = "Delete Selected Profile",
                            TooltipText = "Remove the currently selected private server profile.",
                            Action = onDeleteProfileClicked,
                            Keywords = new[] { "delete", "remove", "profile", "server" },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            savedProfiles = ExternalServerProfile.DeserializeList(config.Get<string>(OsuSetting.ExternalServerProfilesJson));

            // If a legacy CustomApiUrl was already configured before profiles existed, migrate it into a profile
            string existingApi = config.Get<string>(OsuSetting.CustomApiUrl);
            if (!string.IsNullOrWhiteSpace(existingApi) && !ServerHost.IsBlockedPpyHost(existingApi) && savedProfiles.Count == 0)
            {
                var migrated = new ExternalServerProfile
                {
                    Name = existingApi,
                    ApiUrl = existingApi,
                    AvatarUrl = config.Get<string>(OsuSetting.CustomAvatarUrl),
                    WebsiteUrl = config.Get<string>(OsuSetting.CustomWebsiteUrl),
                    ClientId = config.Get<string>(OsuSetting.CustomClientId),
                    ClientSecret = config.Get<string>(OsuSetting.CustomClientSecret),
                };
                savedProfiles.Add(migrated);
                config.SetValue(OsuSetting.ExternalServerProfilesJson, ExternalServerProfile.SerializeList(savedProfiles));
                config.SetValue(OsuSetting.ActiveExternalServerProfileId, migrated.Id);
                config.SetValue(OsuSetting.ExternalServerEnabled, true);
            }
            else if (ServerHost.IsBlockedPpyHost(existingApi))
            {
                resetToBuiltInServer();
            }

            isUpdatingToggleProgrammatically = true;
            enableToggleBindable.Value = externalServerEnabledConfig.Value;
            isUpdatingToggleProgrammatically = false;

            detailsContainer.Alpha = externalServerEnabledConfig.Value ? 1 : 0;

            refreshProfileDropdown();
            profileDropdown.Current.BindValueChanged(onProfileSelectionChanged);

            enableToggleBindable.BindValueChanged(onEnableToggleChanged);
        }

        private void onEnableToggleChanged(ValueChangedEvent<bool> e)
        {
            if (isUpdatingToggleProgrammatically)
                return;

            if (e.NewValue)
            {
                if (dialogOverlay != null)
                {
                    // Revert toggle until user confirms the warning dialog
                    isUpdatingToggleProgrammatically = true;
                    enableToggleBindable.Value = false;
                    isUpdatingToggleProgrammatically = false;

                    dialogOverlay.Push(new ExternalServerWarningDialog(() =>
                    {
                        isUpdatingToggleProgrammatically = true;
                        enableToggleBindable.Value = true;
                        isUpdatingToggleProgrammatically = false;

                        externalServerEnabledConfig.Value = true;
                        detailsContainer.FadeIn(200, Easing.OutQuint);
                        config.Save();
                    }));
                }
                else
                {
                    externalServerEnabledConfig.Value = true;
                    detailsContainer.FadeIn(200, Easing.OutQuint);
                    config.Save();
                }
            }
            else
            {
                if (string.IsNullOrEmpty(activeProfileId))
                {
                    disableExternalServers();
                    return;
                }

                requestSwitch(null, disableExternalServers, () =>
                {
                    isUpdatingToggleProgrammatically = true;
                    enableToggleBindable.Value = true;
                    isUpdatingToggleProgrammatically = false;
                });
            }
        }

        private void disableExternalServers()
        {
            externalServerEnabledConfig.Value = false;
            detailsContainer.FadeOut(200, Easing.OutQuint);
            config.Save();
        }

        private void refreshProfileDropdown()
        {
            isUpdatingDropdownProgrammatically = true;

            var items = new List<string> { default_profile_option };
            foreach (var p in savedProfiles)
                items.Add(formatProfileOption(p));
            items.Add(new_profile_option);

            profileDropdown.Items = items;

            string activeId = config.Get<string>(OsuSetting.ActiveExternalServerProfileId);
            var activeProfile = savedProfiles.FirstOrDefault(p => p.Id == activeId);

            if (activeProfile != null)
            {
                profileDropdown.Current.Value = formatProfileOption(activeProfile);
                populateFieldsFromProfile(activeProfile);
            }
            else
            {
                profileDropdown.Current.Value = default_profile_option;
                populateFieldsFromProfile(null);
            }

            isUpdatingDropdownProgrammatically = false;
        }

        private static string formatProfileOption(ExternalServerProfile p)
            => string.IsNullOrWhiteSpace(p.Name) ? $"{p.ApiUrl} [{p.Id[..Math.Min(6, p.Id.Length)]}]" : $"{p.Name} ({p.ApiUrl})";

        private void onProfileSelectionChanged(ValueChangedEvent<string> e)
        {
            if (isUpdatingDropdownProgrammatically)
                return;

            customApiUrlTextBox.ClearNoticeText();
            customAvatarUrlTextBox.ClearNoticeText();

            if (e.NewValue == default_profile_option)
            {
                editingProfileId = null;
                populateFieldsFromProfile(null);

                if (!string.IsNullOrEmpty(activeProfileId))
                    requestSwitch(null, null, refreshProfileDropdown);
                return;
            }

            if (e.NewValue == new_profile_option)
            {
                editingProfileId = null;
                populateFieldsFromProfile(null);
                return;
            }

            var selected = savedProfiles.FirstOrDefault(p => formatProfileOption(p) == e.NewValue);
            if (selected != null)
            {
                populateFieldsFromProfile(selected);

                if (selected.Id != activeProfileId)
                    requestSwitch(selected, null, refreshProfileDropdown);
            }
        }

        private void populateFieldsFromProfile(ExternalServerProfile? profile)
        {
            editingProfileId = profile?.Id;
            profileNameTextBox.Current.Value = profile?.Name ?? string.Empty;
            customApiUrlTextBox.Current.Value = profile?.ApiUrl ?? string.Empty;
            customWebsiteUrlTextBox.Current.Value = profile?.WebsiteUrl ?? string.Empty;
            customAvatarUrlTextBox.Current.Value = profile?.AvatarUrl ?? string.Empty;
            customClientIdTextBox.Current.Value = profile?.ClientId ?? string.Empty;
            customClientSecretTextBox.Current.Value = profile?.ClientSecret ?? string.Empty;
            deleteProfileButton.Enabled.Value = profile != null;
        }

        private void onSaveAndActivateClicked()
        {
            customApiUrlTextBox.ClearNoticeText();
            customAvatarUrlTextBox.ClearNoticeText();

            string rawApi = stripSchemeAndPath(customApiUrlTextBox.Current.Value ?? string.Empty);
            string rawWebsite = stripSchemeAndPath(customWebsiteUrlTextBox.Current.Value ?? string.Empty);
            string rawAvatar = stripSchemeAndPath(customAvatarUrlTextBox.Current.Value ?? string.Empty);

            if (string.IsNullOrWhiteSpace(rawApi))
            {
                customApiUrlTextBox.SetNoticeText("Please enter an API / Server URL (or select Default somtum.fun).", true);
                return;
            }

            if (ServerHost.IsBlockedPpyHost(rawApi)
                || ServerHost.IsBlockedPpyHost(rawWebsite)
                || ServerHost.IsBlockedPpyHost(rawAvatar))
            {
                customApiUrlTextBox.SetNoticeText("Connections to *.ppy.sh (such as osu.ppy.sh) are blocked! Only dev.ppy.sh is allowed.", true);
                return;
            }

            if (!isValidHostPort(rawApi))
            {
                customApiUrlTextBox.SetNoticeText(OnlineSettingsStrings.CustomApiUrlInvalid, true);
                return;
            }

            if (!string.IsNullOrEmpty(rawAvatar) && !isValidHostPort(rawAvatar))
            {
                customAvatarUrlTextBox.SetNoticeText(OnlineSettingsStrings.CustomApiUrlInvalid, true);
                return;
            }

            var profile = savedProfiles.FirstOrDefault(p => p.Id == editingProfileId);
            if (profile == null)
            {
                profile = new ExternalServerProfile();
                savedProfiles.Add(profile);
            }

            profile.Name = string.IsNullOrWhiteSpace(profileNameTextBox.Current.Value)
                ? rawApi
                : profileNameTextBox.Current.Value.Trim();
            profile.ApiUrl = rawApi;
            profile.WebsiteUrl = rawWebsite;
            profile.AvatarUrl = rawAvatar;
            profile.ClientId = (customClientIdTextBox.Current.Value ?? string.Empty).Trim();
            profile.ClientSecret = (customClientSecretTextBox.Current.Value ?? string.Empty).Trim();

            if (!profile.Validate(out string validationError))
            {
                customApiUrlTextBox.SetNoticeText(validationError, true);
                return;
            }

            editingProfileId = profile.Id;
            saveProfiles();
            refreshProfileDropdown();

            requestSwitch(profile, null, () =>
                customApiUrlTextBox.SetNoticeText("Profile saved. It becomes active once you switch to it (TumThai restarts).", false));
        }

        private void onDeleteProfileClicked()
        {
            if (string.IsNullOrEmpty(editingProfileId))
                return;

            string deletedId = editingProfileId;

            if (deletedId != activeProfileId)
            {
                savedProfiles.RemoveAll(p => p.Id == deletedId);
                editingProfileId = null;
                saveProfiles();
                refreshProfileDropdown();
                customApiUrlTextBox.SetNoticeText("Profile deleted.", false);
                return;
            }

            // the active server's profile: move back to somtum.fun first (keeping nothing of the deleted server's sign-in).
            requestSwitch(null, () =>
            {
                savedProfiles.RemoveAll(p => p.Id == deletedId);
                editingProfileId = null;
                saveProfiles();
            }, refreshProfileDropdown);
        }

        /// <summary>
        /// Asks to switch to <paramref name="target"/> (null = the built-in server). On yes the current server's sign-in
        /// is put aside, the target's saved one is restored (so there's no new login), and TumThai restarts.
        /// </summary>
        /// <param name="target">The profile to switch to, or null for the built-in somtum.fun server.</param>
        /// <param name="beforeRestart">Runs after the switch is written and before the restart.</param>
        /// <param name="onCancelled">Runs when the player says no.</param>
        private void requestSwitch(ExternalServerProfile? target, Action? beforeRestart, Action? onCancelled)
        {
            string name = target == null ? "somtum.fun" : target.Name;

            void confirmed()
            {
                switchAccount(target);
                beforeRestart?.Invoke();
                config.Save();

                // Always close: a game left running on the old server could save that server's refreshed token over the
                // one just restored for the new server. It reopens by itself where it can (installed releases).
                if (game != null)
                {
                    game.RestartAppWhenExited();
                    game.AttemptExit();
                }
                else
                    customApiUrlTextBox.SetNoticeText("Server switched. Close and reopen TumThai to connect.", false);
            }

            if (dialogOverlay == null)
            {
                confirmed();
                return;
            }

            dialogOverlay.Push(new ConfirmDialog(
                $"Switch to {name}? TumThai closes to connect (and opens again by itself where it can). You stay signed in on each server, so you won't need to log in again.",
                confirmed,
                () => onCancelled?.Invoke()));
        }

        private void switchAccount(ExternalServerProfile? target)
        {
            string currentId = activeProfileId;
            string targetId = target?.Id ?? string.Empty;

            if (currentId != targetId)
            {
                // put the server we're leaving's sign-in aside
                string username = config.Get<string>(OsuSetting.Username);
                string token = config.Get<string>(OsuSetting.Token);

                if (string.IsNullOrEmpty(currentId))
                {
                    config.SetValue(OsuSetting.DefaultServerUsername, username);
                    config.SetValue(OsuSetting.DefaultServerToken, token);
                }
                else if (savedProfiles.FirstOrDefault(p => p.Id == currentId) is ExternalServerProfile current)
                {
                    current.Username = username;
                    current.Token = token;
                }

                // and bring back the target's (empty if never signed in there)
                if (target == null)
                {
                    config.SetValue(OsuSetting.Username, config.Get<string>(OsuSetting.DefaultServerUsername));
                    config.SetValue(OsuSetting.Token, config.Get<string>(OsuSetting.DefaultServerToken));
                    config.SetValue(OsuSetting.DefaultServerToken, string.Empty);
                }
                else
                {
                    config.SetValue(OsuSetting.Username, target.Username);
                    config.SetValue(OsuSetting.Token, target.Token);
                    target.Token = string.Empty;
                }
            }

            if (target == null)
                resetToBuiltInServer();
            else
                applyProfileToConfig(target);
        }

        private void saveProfiles()
        {
            config.SetValue(OsuSetting.ExternalServerProfilesJson, ExternalServerProfile.SerializeList(savedProfiles));
            config.Save();
        }

        private void applyProfileToConfig(ExternalServerProfile profile)
        {
            config.SetValue(OsuSetting.ExternalServerProfilesJson, ExternalServerProfile.SerializeList(savedProfiles));
            config.SetValue(OsuSetting.ActiveExternalServerProfileId, profile.Id);
            config.SetValue(OsuSetting.CustomApiUrl, profile.ApiUrl);
            config.SetValue(OsuSetting.CustomWebsiteUrl, profile.WebsiteUrl);
            config.SetValue(OsuSetting.CustomAvatarUrl, profile.AvatarUrl);
            config.SetValue(OsuSetting.CustomClientId, profile.ClientId);
            config.SetValue(OsuSetting.CustomClientSecret, profile.ClientSecret);
            config.Save();
        }

        private void resetToBuiltInServer()
        {
            config.SetValue(OsuSetting.ExternalServerProfilesJson, ExternalServerProfile.SerializeList(savedProfiles));
            config.SetValue(OsuSetting.ActiveExternalServerProfileId, string.Empty);
            config.SetValue(OsuSetting.CustomApiUrl, string.Empty);
            config.SetValue(OsuSetting.CustomWebsiteUrl, string.Empty);
            config.SetValue(OsuSetting.CustomAvatarUrl, string.Empty);
            config.SetValue(OsuSetting.CustomClientId, string.Empty);
            config.SetValue(OsuSetting.CustomClientSecret, string.Empty);
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

        private static string stripSchemeAndPath(string input)
        {
            string s = Regex.Replace(input.Trim(), @"^\s*https?://", string.Empty, RegexOptions.IgnoreCase);

            int slash = s.IndexOf('/');
            if (slash >= 0)
                s = s.Substring(0, slash);

            return s.TrimEnd('/');
        }

        private partial class ExternalServerWarningDialog : PopupDialog
        {
            public ExternalServerWarningDialog(Action onConfirmed)
            {
                HeaderText = "Enable External Server Connection?";
                BodyText = "TumThai is optimized for somtum.fun only — playing on another private server may be broken! (Note: *.ppy.sh servers like osu.ppy.sh are blocked; only dev.ppy.sh is allowed.)";
                Icon = FontAwesome.Solid.ExclamationTriangle;

                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogDangerousButton
                    {
                        Text = "I understand, enable external servers",
                        Action = onConfirmed,
                    },
                    new PopupDialogCancelButton
                    {
                        Text = "Cancel (stay on somtum.fun)",
                    },
                };
            }
        }
    }
}
