// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;

namespace osu.Game.Online
{
    /// <summary>
    /// TumThai: the custom rulesets the server ranks (staff turn them on and pin a file for each). A play of one submits only
    /// when the ruleset it runs on is that exact file (by SHA-256); Settings installs, updates and removes them. Rulesets live
    /// in the data folder's <c>rulesets</c>, where lazer loads them from at start, so a change takes a restart: an update of a
    /// loaded ruleset is kept as <c>&lt;name&gt;.dll.update</c> (and a removal as <c>&lt;name&gt;.dll.delete</c>) and done by
    /// <see cref="ApplyPending"/> the next time TumThai starts.
    /// </summary>
    public partial class SomtumCustomRulesets : Component
    {
        private const string update_suffix = @".update";
        private const string delete_suffix = @".delete";

        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

        /// <summary>The rulesets the server ranks (empty until read, or when it has none).</summary>
        public IBindableList<APISomtumCustomRuleset> Available => available;

        private readonly BindableList<APISomtumCustomRuleset> available = new BindableList<APISomtumCustomRuleset>();
        private readonly ConcurrentDictionary<string, (long Length, DateTime Written, string Hash)> hashes = new ConcurrentDictionary<string, (long, DateTime, string)>();
        private readonly IBindable<APIState> apiState = new Bindable<APIState>();

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        private bool fetched;

        [BackgroundDependencyLoader]
        private void load()
        {
            apiState.BindTo(api.State);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            apiState.BindValueChanged(state =>
            {
                if (state.NewValue == APIState.Online && !fetched)
                    Refresh();
            }, true);
        }

        /// <summary>Reads the server's list again.</summary>
        public void Refresh()
        {
            var request = new GetSomtumCustomRulesetsRequest();
            request.Success += response => Schedule(() =>
            {
                fetched = true;
                available.Clear();
                available.AddRange(response.Rulesets.Where(r => r.ID > 0 && r.Assembly.Length > 0 && r.Sha256.Length == 64));
            });
            api.Queue(request);
        }

        /// <summary>The server's entry for a ruleset (by its assembly), or null when the server doesn't rank it.</summary>
        public APISomtumCustomRuleset? Find(IRulesetInfo ruleset)
        {
            string? assembly = AssemblyOf(ruleset);
            return assembly == null ? null : available.FirstOrDefault(r => string.Equals(r.Assembly, assembly, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The server's entry for a ruleset when its play can submit (the loaded file is the pinned one), or null.
        /// </summary>
        public APISomtumCustomRuleset? Submittable(IRulesetInfo ruleset)
        {
            var entry = Find(ruleset);
            return entry != null && string.Equals(LoadedHash(entry), entry.Sha256, StringComparison.OrdinalIgnoreCase) ? entry : null;
        }

        /// <summary>Where a ruleset stands here, for Settings.</summary>
        public CustomRulesetState StateOf(APISomtumCustomRuleset entry)
        {
            string path = pathFor(entry);
            if (File.Exists(path + update_suffix))
                return CustomRulesetState.RestartToUpdate;
            if (File.Exists(path + delete_suffix))
                return CustomRulesetState.RestartToRemove;

            string? loaded = LoadedHash(entry);
            if (loaded != null)
                return string.Equals(loaded, entry.Sha256, StringComparison.OrdinalIgnoreCase) ? CustomRulesetState.Installed : CustomRulesetState.UpdateAvailable;

            return File.Exists(path) ? CustomRulesetState.RestartToLoad : CustomRulesetState.NotInstalled;
        }

        /// <summary>The SHA-256 of the file the ruleset was loaded from this session, or null when it isn't loaded.</summary>
        public string? LoadedHash(APISomtumCustomRuleset entry)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                                    .FirstOrDefault(a => string.Equals(a.GetName().Name, entry.Assembly, StringComparison.OrdinalIgnoreCase));
            string? location = assembly?.Location;
            return string.IsNullOrEmpty(location) || !File.Exists(location) ? null : hashOf(location);
        }

        /// <summary>
        /// Downloads the pinned file and checks it. It takes the ruleset's place now if it isn't loaded, or on the next
        /// start if it is (Windows keeps a loaded DLL locked).
        /// </summary>
        public async Task InstallAsync(APISomtumCustomRuleset entry, CancellationToken cancellationToken = default)
        {
            string path = pathFor(entry);
            string download = path + @".download";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using (var response = await http.GetAsync(entry.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var file = File.Create(download);
                await using (file.ConfigureAwait(false))
                    await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            string hash = hashOf(download, cached: false);
            if (!string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(download);
                throw new InvalidOperationException($@"The downloaded {entry.Name} isn't the file the server pinned. Try again later.");
            }

            File.Delete(path + delete_suffix);
            if (LoadedHash(entry) != null)
            {
                File.Move(download, path + update_suffix, true);
                return;
            }

            File.Move(download, path, true);
        }

        /// <summary>Removes the ruleset: now if it isn't loaded, on the next start if it is.</summary>
        public void Remove(APISomtumCustomRuleset entry)
        {
            string path = pathFor(entry);
            File.Delete(path + update_suffix);
            if (LoadedHash(entry) != null)
            {
                File.WriteAllText(path + delete_suffix, string.Empty);
                return;
            }

            File.Delete(path);
        }

        /// <summary>The assembly a ruleset is in (from its instantiation info), or null.</summary>
        public static string? AssemblyOf(IRulesetInfo ruleset)
        {
            string[] parts = ruleset.InstantiationInfo.Split(',');
            return parts.Length > 1 ? parts[1].Trim() : null;
        }

        /// <summary>
        /// Does what Settings left for the next start, before the rulesets are loaded: <c>x.dll.update</c> replaces
        /// <c>x.dll</c>, and <c>x.dll.delete</c> removes it.
        /// </summary>
        public static void ApplyPending(string rulesetDirectory)
        {
            if (!Directory.Exists(rulesetDirectory))
                return;

            foreach (string pending in Directory.GetFiles(rulesetDirectory, @"*.dll" + update_suffix))
            {
                try
                {
                    File.Move(pending, pending[..^update_suffix.Length], true);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Couldn't update the ruleset {Path.GetFileName(pending)}");
                }
            }

            foreach (string pending in Directory.GetFiles(rulesetDirectory, @"*.dll" + delete_suffix))
            {
                try
                {
                    File.Delete(pending[..^delete_suffix.Length]);
                    File.Delete(pending);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Couldn't remove the ruleset {Path.GetFileName(pending)}");
                }
            }
        }

        private string pathFor(APISomtumCustomRuleset entry) =>
            storage.GetFullPath(Path.Combine(@"rulesets", Path.GetFileName(entry.Assembly) + @".dll"));

        private string hashOf(string path, bool cached = true)
        {
            var info = new FileInfo(path);
            if (cached && hashes.TryGetValue(path, out var known) && known.Length == info.Length && known.Written == info.LastWriteTimeUtc)
                return known.Hash;

            using var stream = File.OpenRead(path);
            string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (cached)
                hashes[path] = (info.Length, info.LastWriteTimeUtc, hash);
            return hash;
        }
    }

    /// <summary>TumThai: where a custom ruleset stands on this computer.</summary>
    public enum CustomRulesetState
    {
        NotInstalled,
        Installed,
        UpdateAvailable,
        RestartToLoad,
        RestartToUpdate,
        RestartToRemove,
    }
}
