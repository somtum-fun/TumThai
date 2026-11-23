// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Logging;
using osu.Game.Online;
using osu.Game.Online.API;

namespace osu.Game.Utils
{
    /// <summary>
    /// TumThai: sends the errors players hit to the TomYum server they play on (<c>POST /web/tumthai-error</c>), where staff
    /// see them in the admin panel. Only release builds on a TomYum server; each kind of error once a session, at most
    /// <see cref="max_reports_per_session"/>, in the background. No beatmaps, settings or files: the exception, the
    /// game's message, the version and the platform (and who, from the player's token, when signed in).
    /// </summary>
    public class TumThaiErrorReporter : IDisposable
    {
        public const string CLIENT = @"tumthai";

        private const int max_reports_per_session = 20;
        private const int max_stack_trace = 8 * 1024;

        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        private readonly OsuGame game;
        private readonly HashSet<string> sent = new HashSet<string>();
        private readonly bool allowed;

        public TumThaiErrorReporter(OsuGame game)
        {
            this.game = game;

            allowed = game.IsDeployedBuild
                      && !DebugUtils.IsNUnitRunning
                      && Environment.GetEnvironmentVariable("OSU_DISABLE_ERROR_REPORTING") != "1";

            if (allowed)
                Logger.NewEntry += processLogEntry;
        }

        /// <summary>
        /// Whether errors are sent now (a release build, on a TomYum server). The error notification only says it was
        /// reported when this is on.
        /// </summary>
        public bool Enabled => allowed && TumThaiClient.IsTomYumServer && !game.CreateEndpoints().WebsiteUrl.EndsWith(@".ppy.sh", StringComparison.Ordinal);

        private void processLogEntry(LogEntry entry)
        {
            var exception = entry.Exception;

            if (exception == null || exception is SentryOnlyDiagnosticsException || !Enabled || !SentryLogger.ShouldSubmitException(exception))
                return;

            string type = exception.GetType().FullName ?? exception.GetType().Name;
            string stackTrace = exception.ToString();
            if (stackTrace.Length > max_stack_trace)
                stackTrace = stackTrace[..max_stack_trace];

            lock (sent)
            {
                if (sent.Count >= max_reports_per_session || !sent.Add($"{type}\n{entry.Message}\n{exception.StackTrace?.Split('\n')[0]}"))
                    return;
            }

            var report = new
            {
                client = CLIENT,
                version = game.Version,
                platform = $"{RuntimeInfo.OS} {Environment.OSVersion.Version}",
                exception_type = type,
                message = exception.Message,
                stack_trace = stackTrace,
                context = entry.Message,
            };

            string url = $"{game.CreateEndpoints().WebsiteUrl}/web/tumthai-error";
            string? token = game.Dependencies.Get<IAPIProvider>().AccessToken;

            Task.Run(async () =>
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(JsonConvert.SerializeObject(report), Encoding.UTF8, @"application/json"),
                    };
                    if (!string.IsNullOrEmpty(token))
                        request.Headers.Authorization = new AuthenticationHeaderValue(@"Bearer", token);

                    using var response = await http.SendAsync(request).ConfigureAwait(false);
                }
                catch
                {
                    // Not logged as an error: that would be reported in turn. The error itself is in the log already.
                }
            });
        }

        public void Dispose()
        {
            if (allowed)
                Logger.NewEntry -= processLogEntry;
        }
    }
}
