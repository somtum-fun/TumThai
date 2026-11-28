// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using osu.Game.Online;

namespace osu.Desktop
{
    /// <summary>
    /// The desktop's machine details for <see cref="TumThaiClient"/>. Only read, never written: osu!(stable)'s
    /// uninstall id is used when stable made one, and the operating system's own machine id.
    /// </summary>
    public class TumThaiDesktopMachine : ITumThaiMachineInfo
    {
        public string? StableUninstallId
        {
            get
            {
                if (!OperatingSystem.IsWindows())
                    return null;

                using var key = Registry.CurrentUser.OpenSubKey(@"Software\osu!");
                return key?.GetValue(@"UninstallID") as string;
            }
        }

        public string? MachineId
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                    return key?.GetValue(@"MachineGuid") as string;
                }

                if (OperatingSystem.IsLinux())
                {
                    foreach (string file in new[] { @"/etc/machine-id", @"/var/lib/dbus/machine-id" })
                    {
                        if (File.Exists(file))
                            return File.ReadAllText(file).Trim();
                    }
                }

                // TumThai: macOS's hardware UUID, which stays the same across reinstalls like the two above.
                if (OperatingSystem.IsMacOS())
                    return macPlatformUuid();

                return null;
            }
        }

        /// <summary>macOS: IOPlatformUUID (System Information's "Hardware UUID") from <c>ioreg</c>, or null.</summary>
        private static string? macPlatformUuid()
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(@"/usr/sbin/ioreg", @"-rd1 -c IOPlatformExpertDevice")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                if (process == null)
                    return null;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(2000);
                return MacPlatformUuidFrom(output);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The <c>"IOPlatformUUID" = "…"</c> value in <c>ioreg -rd1 -c IOPlatformExpertDevice</c>'s output.</summary>
        public static string? MacPlatformUuidFrom(string ioregOutput)
        {
            var match = Regex.Match(ioregOutput, "\"IOPlatformUUID\"\\s*=\\s*\"([0-9A-Fa-f-]{36})\"");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
