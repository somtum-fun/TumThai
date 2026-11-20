// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Framework.Logging;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Metadata;
using osu.Game.Online.Spectator;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Replays;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Users;
using Realms;
using System.Threading.Tasks;

namespace osu.Game.Screens.Spectate
{
    /// <summary>
    /// A <see cref="OsuScreen"/> which spectates one or more users.
    /// </summary>
    public abstract partial class SpectatorScreen : OsuScreen
    {
        protected IReadOnlyList<int> Users => users;

        private readonly List<int> users = new List<int>();

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        /// <summary>TumThai: the custom rulesets already offered here, so each is asked about once.</summary>
        private readonly HashSet<int> offeredRulesets = new HashSet<int>();

        [Resolved]
        private SpectatorClient spectatorClient { get; set; } = null!;

        [Resolved]
        private MetadataClient metadataClient { get; set; } = null!;

        [Resolved]
        private UserLookupCache userLookupCache { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private readonly IBindableDictionary<int, SpectatorState> userStates = new BindableDictionary<int, SpectatorState>();

        private readonly Dictionary<int, APIUser> userMap = new Dictionary<int, APIUser>();
        private readonly Dictionary<int, SpectatorGameplayState> gameplayStates = new Dictionary<int, SpectatorGameplayState>();

        private IDisposable? realmSubscription;
        private IDisposable? userWatchToken;

        /// <summary>
        /// Creates a new <see cref="SpectatorScreen"/>.
        /// </summary>
        /// <param name="users">The users to spectate.</param>
        protected SpectatorScreen(params int[] users)
        {
            this.users.AddRange(users);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            userWatchToken = metadataClient.BeginWatchingUserPresence();

            userLookupCache.GetUsersAsync(users.ToArray()).ContinueWith(task => Schedule(() =>
            {
                var foundUsers = task.GetResultSafely();

                foreach (var u in foundUsers)
                {
                    if (u == null)
                        continue;

                    userMap[u.Id] = u;
                }

                userStates.BindTo(spectatorClient.WatchedUserStates);
                userStates.BindCollectionChanged(onUserStatesChanged, true);

                realmSubscription = realm.RegisterForNotifications(
                    realm => realm.All<BeatmapSetInfo>().Where(s => !s.DeletePending), beatmapsChanged);

                foreach ((int id, var _) in userMap)
                    spectatorClient.WatchUser(id);
            }));
        }

        private void beatmapsChanged(IRealmCollection<BeatmapSetInfo> items, ChangeSet? changes)
        {
            if (changes?.InsertedIndices == null) return;

            foreach (int c in changes.InsertedIndices) beatmapUpdated(items[c]);
        }

        private void beatmapUpdated(BeatmapSetInfo beatmapSet)
        {
            foreach ((int userId, _) in userMap)
            {
                if (!userStates.TryGetValue(userId, out var userState))
                    continue;

                if (beatmapSet.Beatmaps.Any(b => b.OnlineID == userState.BeatmapID))
                    startGameplay(userId);
            }
        }

        private void onUserStatesChanged(object? sender, NotifyDictionaryChangedEventArgs<int, SpectatorState> e)
        {
            switch (e.Action)
            {
                case NotifyDictionaryChangedAction.Add:
                case NotifyDictionaryChangedAction.Replace:
                    foreach ((int userId, SpectatorState state) in e.NewItems.AsNonNull())
                        onUserStateChanged(userId, state);
                    break;
            }
        }

        private void onUserStateChanged(int userId, SpectatorState newState)
        {
            if (newState.RulesetID == null || newState.BeatmapID == null)
                return;

            if (!userMap.ContainsKey(userId))
                return;

            switch (newState.State)
            {
                case SpectatedUserState.Playing:
                    OnNewPlayingUserState(userId, newState);
                    startGameplay(userId);
                    break;

                case SpectatedUserState.Passed:
                    markReceivedAllFrames(userId);
                    PassGameplay(userId);
                    break;

                case SpectatedUserState.Failed:
                    failGameplay(userId);
                    break;

                case SpectatedUserState.Quit:
                    quitGameplay(userId);
                    break;
            }
        }

        private void startGameplay(int userId)
        {
            Debug.Assert(userMap.ContainsKey(userId));

            var user = userMap[userId];
            var spectatorState = userStates[userId];

            // TumThai: a custom ruleset goes by the server's id; one that isn't here is offered for download.
            var resolvedRuleset = SomtumCustomRulesets.Resolve(rulesets, spectatorState.RulesetID!.Value)?.CreateInstance();
            if (resolvedRuleset == null)
            {
                offerMissingRuleset(userId, spectatorState.RulesetID.Value);
                return;
            }

            var resolvedBeatmap = beatmaps.QueryBeatmap(b => b.OnlineID == spectatorState.BeatmapID);
            if (resolvedBeatmap == null)
                return;

            var score = new Score
            {
                ScoreInfo = new ScoreInfo
                {
                    BeatmapInfo = resolvedBeatmap,
                    User = user,
                    Mods = spectatorState.Mods.Select(m => m.ToMod(resolvedRuleset)).ToArray(),
                    Ruleset = resolvedRuleset.RulesetInfo,
                    // TumThai: an osu!(stable) player's play (relayed by TomYum's spectator server) is a stable replay: its
                    // Relax frames have no presses, so Relax presses from the cursor as for a stable replay.
                    IsLegacyScore = isOnStable(userId),
                },
                Replay = new Replay { HasReceivedAllFrames = false },
            };

            var gameplayState = new SpectatorGameplayState(score, resolvedRuleset, beatmaps.GetWorkingBeatmap(resolvedBeatmap));

            gameplayStates[userId] = gameplayState;
            StartGameplay(userId, gameplayState);
        }

        /// <summary>TumThai: the player plays on osu!(stable) (TomYum's spectator server gives them its "osu!(stable)" activity).</summary>
        private bool isOnStable(int userId) =>
            metadataClient.GetPresence(userId)?.Activity is UserActivity.InSoloGame { RulesetPlayingVerb: var verb }
            && verb.StartsWith(@"Playing on osu!(stable)", StringComparison.Ordinal);

        /// <summary>
        /// TumThai: the spectated player is on a custom ruleset the server ranks that isn't here (or not the server's version
        /// of it): asks whether to download it. Once it's loaded (no restart for a new one), watching starts.
        /// </summary>
        private void offerMissingRuleset(int userId, int rulesetId)
        {
            var custom = SomtumCustomRulesets.Current;
            var entry = custom?.EntryFor(rulesetId);
            if (custom == null || entry == null || !offeredRulesets.Add(rulesetId))
                return;

            string username = userMap.TryGetValue(userId, out var user) ? user.Username : "This player";

            switch (custom.StateOf(entry))
            {
                case CustomRulesetState.RestartToLoad:
                case CustomRulesetState.RestartToUpdate:
                    notifications?.Post(new SimpleNotification { Text = $"{username} is playing {entry.Name}. Restart TumThai to load it, then watch again." });
                    return;

                case CustomRulesetState.Broken:
                case CustomRulesetState.RestartToRemove:
                    notifications?.Post(new SimpleErrorNotification { Text = $"{username} is playing {entry.Name}, which can't be watched here: see Settings > Custom rulesets." });
                    return;
            }

            if (dialogOverlay == null)
                return;

            bool update = custom.StateOf(entry) == CustomRulesetState.UpdateAvailable;
            dialogOverlay.Push(new ConfirmDialog(
                update
                    ? $"{username} is playing {entry.Name}, and yours isn't the server's version. Download it? (It's used after you restart TumThai.)"
                    : $"{username} is playing {entry.Name}, a custom ruleset you don't have. Download it to watch?",
                () => Task.Run(() => custom.InstallAsync(entry)).ContinueWith(t => Schedule(() =>
                {
                    if (t.IsFaulted)
                    {
                        Logger.Error(t.Exception, $@"Couldn't install the {entry.Name} ruleset");
                        notifications?.Post(new SimpleErrorNotification { Text = $"Couldn't download {entry.Name}: {t.Exception?.GetBaseException().Message}" });
                        return;
                    }

                    if (!t.GetResultSafely())
                    {
                        notifications?.Post(new SimpleNotification { Text = $"{entry.Name} is downloaded. Restart TumThai to use it, then watch again." });
                        return;
                    }

                    if (userStates.TryGetValue(userId, out var state) && state.State == SpectatedUserState.Playing)
                        startGameplay(userId);
                }))));
        }

        /// <summary>
        /// Marks an existing gameplay session as received all frames.
        /// </summary>
        private void markReceivedAllFrames(int userId)
        {
            if (gameplayStates.TryGetValue(userId, out var gameplayState))
                gameplayState.Score.Replay.HasReceivedAllFrames = true;
        }

        private void failGameplay(int userId)
        {
            if (!userMap.ContainsKey(userId))
                return;

            if (!gameplayStates.ContainsKey(userId))
                return;

            markReceivedAllFrames(userId);

            gameplayStates.Remove(userId);
            FailGameplay(userId);
        }

        private void quitGameplay(int userId)
        {
            if (!userMap.ContainsKey(userId))
                return;

            if (!gameplayStates.ContainsKey(userId))
                return;

            markReceivedAllFrames(userId);

            gameplayStates.Remove(userId);
            QuitGameplay(userId);
        }

        /// <summary>
        /// Invoked when a spectated user's state has changed to a new state indicating the player is currently playing.
        /// Thread safety is not guaranteed – should be scheduled as required.
        /// </summary>
        /// <param name="userId">The user whose state has changed.</param>
        /// <param name="spectatorState">The new state.</param>
        protected abstract void OnNewPlayingUserState(int userId, SpectatorState spectatorState);

        /// <summary>
        /// Starts gameplay for a user.
        /// Thread safety is not guaranteed – should be scheduled as required.
        /// </summary>
        /// <param name="userId">The user to start gameplay for.</param>
        /// <param name="spectatorGameplayState">The gameplay state.</param>
        protected abstract void StartGameplay(int userId, SpectatorGameplayState spectatorGameplayState);

        /// <summary>
        /// Fired when a user passes gameplay.
        /// </summary>
        /// <param name="userId">The user which passed.</param>
        protected virtual void PassGameplay(int userId) { }

        /// <summary>
        /// Quits gameplay for a user.
        /// Thread safety is not guaranteed – should be scheduled as required.
        /// </summary>
        /// <param name="userId">The user to quit gameplay for.</param>
        protected abstract void QuitGameplay(int userId);

        /// <summary>
        /// Fails gameplay for a user.
        /// Thread safety is not guaranteed – should be scheduled as required.
        /// </summary>
        /// <param name="userId">The user to fail gameplay for.</param>
        protected abstract void FailGameplay(int userId);

        /// <summary>
        /// Stops spectating a user.
        /// </summary>
        /// <param name="userId">The user to stop spectating.</param>
        protected void RemoveUser(int userId)
        {
            if (!userStates.ContainsKey(userId))
                return;

            quitGameplay(userId);

            users.Remove(userId);
            userMap.Remove(userId);

            spectatorClient.StopWatchingUser(userId);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (spectatorClient.IsNotNull())
            {
                foreach ((int userId, var _) in userMap)
                    spectatorClient.StopWatchingUser(userId);
            }

            realmSubscription?.Dispose();
            userWatchToken?.Dispose();
        }
    }
}
