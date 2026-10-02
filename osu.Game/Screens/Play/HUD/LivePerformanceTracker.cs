// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// osu!somtum: works out the live pp of the current play, so the pp counter and the gameplay leaderboard share one calculation.
    /// Nothing is calculated until <see cref="Start"/> is called by something that needs it.
    /// </summary>
    public partial class LivePerformanceTracker : Component
    {
        /// <summary>
        /// The pp of the play so far (not rounded).
        /// </summary>
        public IBindable<double> Performance => performance;

        private readonly BindableDouble performance = new BindableDouble();

        /// <summary>
        /// Whether <see cref="Performance"/> currently holds a calculated value.
        /// </summary>
        public IBindable<bool> IsValid => isValid;

        private readonly BindableBool isValid = new BindableBool();

        [Resolved]
        private ScoreProcessor? scoreProcessor { get; set; }

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private List<TimedDifficultyAttributes>? timedAttributes;

        private readonly CancellationTokenSource loadCancellationSource = new CancellationTokenSource();

        private JudgementResult? lastJudgement;
        private PerformanceCalculator? performanceCalculator;
        private ScoreInfo? scoreInfo;

        private int started;

        /// <summary>
        /// Starts the calculation. Safe to call more than once.
        /// </summary>
        public void Start()
        {
            if (Interlocked.Exchange(ref started, 1) == 1)
                return;

            if (gameplayState == null)
                return;

            performanceCalculator = gameplayState.Ruleset.CreatePerformanceCalculator();
            var clonedMods = gameplayState.Mods.Select(m => m.DeepClone()).ToArray();

            scoreInfo = new ScoreInfo(gameplayState.Score.ScoreInfo.BeatmapInfo, gameplayState.Score.ScoreInfo.Ruleset) { Mods = clonedMods };

            var gameplayWorkingBeatmap = new GameplayWorkingBeatmap(gameplayState.Beatmap);
            difficultyCache.GetTimedDifficultyAttributesAsync(gameplayWorkingBeatmap, gameplayState.Ruleset, clonedMods, loadCancellationSource.Token)
                           .ContinueWith(task => Schedule(() =>
                           {
                               timedAttributes = task.GetResultSafely();

                               isValid.Value = true;

                               if (lastJudgement != null)
                                   onJudgementChanged(lastJudgement);
                           }), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (scoreProcessor != null)
            {
                scoreProcessor.NewJudgement += onJudgementChanged;
                scoreProcessor.JudgementReverted += onJudgementChanged;
            }

            if (gameplayState?.LastJudgementResult.Value != null)
                onJudgementChanged(gameplayState.LastJudgementResult.Value);
        }

        private void onJudgementChanged(JudgementResult judgement)
        {
            lastJudgement = judgement;

            // not started yet (or still loading attributes).
            if (timedAttributes == null)
                return;

            var attrib = getAttributeAtTime(judgement);

            if (gameplayState == null || attrib == null || scoreProcessor == null || scoreInfo == null)
            {
                isValid.Value = false;
                return;
            }

            scoreProcessor.PopulateScore(scoreInfo);
            performance.Value = performanceCalculator?.Calculate(scoreInfo, attrib).Total ?? 0;
            isValid.Value = true;
        }

        private DifficultyAttributes? getAttributeAtTime(JudgementResult judgement)
        {
            if (timedAttributes == null || timedAttributes.Count == 0)
                return null;

            int attribIndex = timedAttributes.BinarySearch(new TimedDifficultyAttributes(judgement.HitObject.GetEndTime(), null!));
            if (attribIndex < 0)
                attribIndex = ~attribIndex - 1;

            return timedAttributes[Math.Clamp(attribIndex, 0, timedAttributes.Count - 1)].Attributes;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (scoreProcessor != null)
            {
                scoreProcessor.NewJudgement -= onJudgementChanged;
                scoreProcessor.JudgementReverted -= onJudgementChanged;
            }

            loadCancellationSource.Cancel();
        }

        // TODO: This class shouldn't exist, but requires breaking changes to allow DifficultyCalculator to receive an IBeatmap.
        private class GameplayWorkingBeatmap : WorkingBeatmap
        {
            private readonly IBeatmap gameplayBeatmap;

            public GameplayWorkingBeatmap(IBeatmap gameplayBeatmap)
                : base(gameplayBeatmap.BeatmapInfo, null)
            {
                this.gameplayBeatmap = gameplayBeatmap;
            }

            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken)
                => gameplayBeatmap;

            protected override IBeatmap GetBeatmap() => gameplayBeatmap;

            public override Texture GetBackground() => throw new NotImplementedException();

            protected override Track GetBeatmapTrack() => throw new NotImplementedException();

            protected internal override ISkin GetSkin() => throw new NotImplementedException();

            public override Stream GetStream(string storagePath) => throw new NotImplementedException();
        }
    }
}
