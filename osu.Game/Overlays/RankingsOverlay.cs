// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Extensions;
using osu.Game.Online;
using osu.Game.Overlays.Rankings;
using osu.Game.Users;
using osu.Game.Rulesets;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays.Rankings.Tables;

namespace osu.Game.Overlays
{
    public partial class RankingsOverlay : TabbableOnlineOverlay<RankingsOverlayHeader, RankingsScope>
    {
        protected Bindable<CountryCode> Country => Header.Country;

        private APIRequest lastRequest;

        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> parentRuleset { get; set; }

        // TumThai: a custom ruleset's rankings are asked for by its slug on the server.
        [Resolved(CanBeNull = true)]
        private SomtumCustomRulesets customRulesets { get; set; }

        [Cached]
        private readonly Bindable<RulesetInfo> ruleset = new Bindable<RulesetInfo>();

        public RankingsOverlay()
            : base(OverlayColourScheme.Green)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Header.Ruleset.BindTo(ruleset);

            Country.BindValueChanged(_ =>
            {
                // if a country is requested, force performance scope.
                if (!Country.IsDefault)
                    Header.Current.Value = RankingsScope.Performance;

                Scheduler.AddOnce(triggerTabChanged);
            });

            ruleset.BindValueChanged(_ =>
            {
                if (Header.Current.Value == RankingsScope.Playlists)
                    return;

                Scheduler.AddOnce(triggerTabChanged);
            });
        }

        private bool requiresRulesetUpdate = true;

        protected override void PopIn()
        {
            if (requiresRulesetUpdate)
            {
                ruleset.Value = parentRuleset.Value;
                requiresRulesetUpdate = false;
            }

            base.PopIn();
        }

        protected override void OnTabChanged(RankingsScope tab)
        {
            // country filtering is only valid for performance scope.
            if (Header.Current.Value != RankingsScope.Performance)
                Country.SetDefault();

            Scheduler.AddOnce(triggerTabChanged);
        }

        private void triggerTabChanged() => base.OnTabChanged(Header.Current.Value);

        protected override RankingsOverlayHeader CreateHeader() => new RankingsOverlayHeader();

        public void ShowCountry(CountryCode requested)
        {
            if (requested == default)
                return;

            Show();

            Country.Value = requested;
        }

        protected override void CreateDisplayToLoad(RankingsScope tab)
        {
            lastRequest?.Cancel();

            if (Header.Current.Value == RankingsScope.Playlists)
            {
                LoadDisplay(new SpotlightsLayout
                {
                    Ruleset = { BindTarget = ruleset }
                });
                return;
            }

            var request = createScopedRequest();
            lastRequest = request;

            if (request == null)
            {
                LoadDisplay(Empty());
                return;
            }

            request.Success += () => Schedule(() => LoadDisplay(createTableFromResponse(request)));
            request.Failure += _ => Schedule(() => LoadDisplay(Empty()));

            api.Queue(request);
        }

        private APIRequest createScopedRequest()
        {
            // TumThai: a custom ruleset has one ranking, by ranked score (no pp, no countries' table); none when the
            // server doesn't rank it.
            if (ruleset.Value != null && !ruleset.Value.IsLegacyRuleset() && !ruleset.Value.IsSpecialRuleset())
            {
                string slug = customRulesets?.Find(ruleset.Value)?.Slug;

                if (slug == null || Header.Current.Value is not (RankingsScope.Performance or RankingsScope.Score))
                    return null;

                return new GetUserRankingsRequest(ruleset.Value, UserRankingsType.Score, countryCode: Country.Value) { SomtumMode = slug };
            }

            switch (Header.Current.Value)
            {
                case RankingsScope.Performance:
                    return new GetUserRankingsRequest(ruleset.Value, countryCode: Country.Value);

                case RankingsScope.Country:
                    return new GetCountryRankingsRequest(ruleset.Value);

                case RankingsScope.Score:
                    return new GetUserRankingsRequest(ruleset.Value, UserRankingsType.Score);

                case RankingsScope.Kudosu:
                    return new GetKudosuRankingsRequest();
            }

            return null;
        }

        private Drawable createTableFromResponse(APIRequest request)
        {
            switch (request)
            {
                case GetUserRankingsRequest userRequest:
                    if (userRequest.Response == null)
                        return null;

                    switch (userRequest.Type)
                    {
                        case UserRankingsType.Performance:
                            return new PerformanceTable(1, userRequest.Response.Users);

                        case UserRankingsType.Score:
                            return new ScoresTable(1, userRequest.Response.Users);
                    }

                    return null;

                case GetCountryRankingsRequest countryRequest:
                {
                    if (countryRequest.Response == null)
                        return null;

                    return new CountriesTable(1, countryRequest.Response.Countries);
                }

                case GetKudosuRankingsRequest kudosuRequest:
                    if (kudosuRequest.Response == null)
                        return null;

                    return new KudosuTable(1, kudosuRequest.Response.Users);
            }

            return null;
        }

        protected override void Dispose(bool isDisposing)
        {
            lastRequest?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
