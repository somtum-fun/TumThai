// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using osu.Framework.Input;
using osu.Framework.Input.States;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Replays;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Scoring.Legacy
{
    /// <summary>
    /// A minified version of <see cref="SoloScoreInfo"/> retrofit onto the end of legacy replay files (.osr),
    /// containing the minimum data required to support storage of non-legacy replays.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public class LegacyReplaySoloScoreInfo
    {
        /// <remarks>
        /// The value of this property should correspond to <see cref="ScoreInfo.OnlineID"/>
        /// (i.e. come from the `solo_scores` ID scheme).
        /// </remarks>
        [JsonProperty("online_id")]
        public long OnlineID { get; set; } = -1;

        [JsonProperty("mods")]
        public APIMod[] Mods { get; set; } = Array.Empty<APIMod>();

        [JsonProperty("statistics")]
        public Dictionary<HitResult, int> Statistics { get; set; } = new Dictionary<HitResult, int>();

        [JsonProperty("maximum_statistics")]
        public Dictionary<HitResult, int> MaximumStatistics { get; set; } = new Dictionary<HitResult, int>();

        [JsonProperty("client_version")]
        public string ClientVersion = string.Empty;

        [JsonProperty("rank")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ScoreRank? Rank;

        [JsonProperty("user_id")]
        public int UserID = -1;

        [JsonProperty("total_score_without_mods")]
        public long? TotalScoreWithoutMods { get; set; }

        [JsonProperty("pauses")]
        public int[] Pauses { get; set; } = [];

        /// <summary>
        /// TumThai: compact delta-encoded multi-touch stream for local custom-ruleset replays (e.g. Sentakki on mobile).
        /// </summary>
        [JsonProperty("tumthai_touch_replay", NullValueHandling = NullValueHandling.Ignore)]
        public string? CustomTouchReplay { get; set; }

        internal static string EncodeTouchFrames(IReadOnlyList<ReplayTouchFrame> frames, double timingOffset)
        {
            var sb = new StringBuilder();
            int lastTime = 0;

            for (int i = 0; i < frames.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                var frame = frames[i];
                int time = (int)Math.Round(frame.Time + timingOffset);

                if (i > 0)
                {
                    // Ensure sub-millisecond flick taps do not collapse down+up into a 0 ms window after integer rounding.
                    if (time <= lastTime && frame.Touches.Length == 0 && frames[i - 1].Touches.Length > 0)
                        time = lastTime + 1;
                    else if (time < lastTime)
                        time = lastTime;
                }

                sb.Append(FormattableString.Invariant($"{time - lastTime}:"));
                lastTime = time;

                for (int j = 0; j < frame.Touches.Length; j++)
                {
                    if (j > 0)
                        sb.Append(';');

                    var t = frame.Touches[j];
                    sb.Append(FormattableString.Invariant($"{(int)t.Source}|{t.Position.X:0.##}|{t.Position.Y:0.##}"));
                }
            }

            return sb.ToString();
        }

        internal static List<ReplayTouchFrame> DecodeTouchFrames(string encoded, long beatmapOffset)
        {
            var result = new List<ReplayTouchFrame>();
            if (string.IsNullOrEmpty(encoded))
                return result;

            long lastTime = beatmapOffset;
            string[] entries = encoded.Split(',');

            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i];
                int colon = entry.IndexOf(':');
                if (colon <= 0)
                    continue;

                if (!int.TryParse(entry.AsSpan(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int diff))
                    continue;

                lastTime += diff;

                ReadOnlySpan<char> payload = entry.AsSpan(colon + 1);

                if (payload.IsEmpty)
                {
                    result.Add(new ReplayTouchFrame(lastTime, Array.Empty<ReplayTouchPoint>()));
                    continue;
                }

                string[] touchParts = payload.ToString().Split(';');
                var points = new List<ReplayTouchPoint>(touchParts.Length);

                for (int j = 0; j < touchParts.Length; j++)
                {
                    string[] coords = touchParts[j].Split('|');
                    if (coords.Length < 3)
                        continue;

                    if (!int.TryParse(coords[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int src)
                        || (uint)src >= (uint)TouchState.MAX_TOUCH_COUNT)
                        continue;

                    if (!float.TryParse(coords[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                        || !float.TryParse(coords[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                        continue;

                    points.Add(new ReplayTouchPoint((TouchSource)src, new Vector2(x, y)));
                }

                result.Add(new ReplayTouchFrame(lastTime, points.ToArray()));
            }

            return result;
        }

        public static LegacyReplaySoloScoreInfo FromScore(ScoreInfo score) => new LegacyReplaySoloScoreInfo
        {
            OnlineID = score.OnlineID,
            Mods = score.APIMods,
            Statistics = score.Statistics.Where(kvp => kvp.Value != 0).ToDictionary(),
            MaximumStatistics = score.MaximumStatistics.Where(kvp => kvp.Value != 0).ToDictionary(),
            ClientVersion = score.ClientVersion,
            Rank = score.Rank,
            UserID = score.User.OnlineID,
            TotalScoreWithoutMods = score.TotalScoreWithoutMods > 0 ? score.TotalScoreWithoutMods : null,
            Pauses = score.Pauses.ToArray(),
        };
    }
}
