// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Logging;

namespace osu.Desktop
{
    /// <summary>
    /// TumThai: the data folder used to be named "osu-tumthai" ("osu-tumthai-dev" for TumThai Dev). It's "TumThai"
    /// ("TumThaiDev") now: on the first start, the old folder is moved to the new name, in every place the game
    /// keeps its data (%AppData% on Windows, ~/.local/share on Linux, ~/Library/Application Support on macOS).
    /// </summary>
    public static class DataFolderMigration
    {
        /// <summary>
        /// Moves <paramref name="oldName"/>'s folder to <paramref name="newName"/> where only the old one exists.
        /// </summary>
        /// <returns>
        /// The name to run with: <paramref name="newName"/>, or <paramref name="oldName"/> when its folder exists but
        /// couldn't be moved (another TumThai is running and holds its files, for example), so the game never starts
        /// with an empty new folder. It tries again on the next start.
        /// </returns>
        public static string Migrate(string oldName, string newName)
        {
            string result = newName;

            foreach (string root in dataRoots())
            {
                string oldPath = Path.Combine(root, oldName);
                string newPath = Path.Combine(root, newName);

                try
                {
                    if (!Directory.Exists(oldPath) || Directory.Exists(newPath))
                        continue;

                    Directory.Move(oldPath, newPath);
                    Logger.Log($"Moved TumThai's data folder from {oldPath} to {newPath}.");
                }
                catch (Exception e)
                {
                    Logger.Log($"Couldn't move TumThai's data folder from {oldPath} to {newPath} ({e.Message}); using the old one this time.");
                    result = oldName;
                }
            }

            return result;
        }

        // The framework's storage roots (GameHost.UserStoragePaths and the platform overrides).
        private static IEnumerable<string> dataRoots()
        {
            if (OperatingSystem.IsWindows())
            {
                yield return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                yield break;
            }

            yield return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Some older builds of osu! used ~/.local/share on macOS.
            if (OperatingSystem.IsMacOS())
                yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }
    }
}
