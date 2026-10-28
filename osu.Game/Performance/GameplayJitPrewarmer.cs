// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Performance
{
    /// <summary>
    /// Asynchronously pre-JITs hot gameplay, judgement, scoring, audio sample, and ruleset methods
    /// on a background thread after startup so the first beatmap played does not suffer Tier-0 JIT
    /// compilation spikes on the first hit circle, slider, or stream (especially on Android CoreCLR
    /// where ReadyToRun images are disabled).
    /// </summary>
    public static class GameplayJitPrewarmer
    {
        private static int started;

        private static readonly Type[] core_gameplay_types =
        {
            typeof(DrawableHitObject),
            typeof(Judgement),
            typeof(JudgementResult),
            typeof(DrawableJudgement),
            typeof(JudgementProcessor),
            typeof(ScoreProcessor),
            typeof(HealthProcessor),
            typeof(DrainingHealthProcessor),
            typeof(LegacyDrainingHealthProcessor),
            typeof(HitWindows),
            typeof(DefaultHitWindows),
            typeof(SamplePlaybackHelper),
            typeof(HitSampleInfo),
            typeof(GameplayClockContainer),
            typeof(Interpolation),
        };

        /// <summary>
        /// Triggers background JIT pre-warming once per process lifetime.
        /// </summary>
        public static void StartPrewarm()
        {
            if (Interlocked.Exchange(ref started, 1) != 0)
                return;

            Task.Run(() =>
            {
                try
                {
                    if (Thread.CurrentThread.Priority > ThreadPriority.BelowNormal)
                        Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                }
                catch
                {
                }

                var sw = Stopwatch.StartNew();
                int preparedCount = 0;

                foreach (var type in core_gameplay_types)
                    preparedCount += prepareTypeMethods(type);

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string? name = assembly.GetName().Name;
                    if (name == null || !name.StartsWith("osu.Game.Rulesets.", StringComparison.Ordinal) || name.Contains(".Tests", StringComparison.Ordinal))
                        continue;

                    Type[] types;

                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types!;
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var type in types)
                    {
                        if (type == null)
                            continue;

                        string? ns = type.Namespace;
                        if (ns == null)
                            continue;

                        if (ns.Contains(".Objects", StringComparison.Ordinal)
                            || ns.Contains(".Judgements", StringComparison.Ordinal)
                            || ns.Contains(".Scoring", StringComparison.Ordinal)
                            || ns.Contains(".UI", StringComparison.Ordinal))
                        {
                            preparedCount += prepareTypeMethods(type);
                        }
                    }
                }

                sw.Stop();
                Logger.Log($"[JITPrewarm] Pre-compiled {preparedCount} gameplay methods in {sw.ElapsedMilliseconds} ms.", LoggingTarget.Performance);
            });
        }

        private static int prepareTypeMethods(Type type)
        {
            if (type.IsInterface || type.ContainsGenericParameters)
                return 0;

            int count = 0;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            MethodInfo[] methods;

            try
            {
                methods = type.GetMethods(flags);
            }
            catch
            {
                return 0;
            }

            foreach (var method in methods)
            {
                if (method.IsAbstract || method.ContainsGenericParameters)
                    continue;

                try
                {
                    if (method.GetMethodBody() == null)
                        continue;

                    RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    count++;
                }
                catch
                {
                    // Skip methods that cannot be prepared ahead of instantiation (e.g. P/Invoke or platform-specific stubs).
                }
            }

            return count;
        }
    }
}
