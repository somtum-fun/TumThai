// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Scoring;

namespace osu.Game.Online.Rooms
{
    public abstract class SubmitScoreRequest : APIRequest<MultiplayerScore>
    {
        public readonly SoloScoreInfo Score;

        protected readonly long ScoreId;

        protected SubmitScoreRequest(ScoreInfo scoreInfo, long scoreId)
        {
            Score = SoloScoreInfo.ForSubmission(scoreInfo);
            ScoreId = scoreId;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();

            req.ContentType = "application/json";
            req.Method = HttpMethod.Put;
            req.Timeout = 30000;

            string json = JsonConvert.SerializeObject(Score, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            req.AddRaw(json);

            // TumThai score-sig: HMAC(access_token, "somtum-score-v1\n" + Target + "\n" + json_body).
            // Server (osu-web) re-derives the signature from the OAuth token it already holds.
            string? accessToken = API?.AccessToken;

            if (!string.IsNullOrEmpty(accessToken))
            {
                byte[] keyBytes = Encoding.UTF8.GetBytes(accessToken);
                byte[] payload = Encoding.UTF8.GetBytes("somtum-score-v1\n" + Target + "\n" + json);
                using (var hmac = new HMACSHA256(keyBytes))
                {
                    byte[] digest = hmac.ComputeHash(payload);
                    var hex = new StringBuilder(digest.Length * 2);
                    foreach (byte b in digest)
                        hex.Append(b.ToString("x2"));
                    req.AddHeader(@"X-Somtum-Sig", hex.ToString());
                }
            }

            return req;
        }
    }
}
