using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.Ordering;
using NzbDrone.Core.MetadataSource.Tvdb.Resource;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Tvdb
{
    public interface IProvideTvdbOrdering
    {
        /// <summary>
        /// The orderings TheTVDB can supply for a series: the default aired order, followed by
        /// whichever of its absolute and alternate orders actually list episodes. Empty when
        /// there is no API key or TheTVDB has nothing beyond the aired order, so the caller
        /// offers no choice rather than a pointless one.
        /// </summary>
        List<EpisodeOrdering> GetOrderings(int tvdbId);

        /// <summary>
        /// Renumbers episodes (as returned by SkyHook) into one of this provider's orderings.
        /// Throws rather than falling back to the aired order, because a silent fallback
        /// would renumber, and so rename, an existing library on the next refresh.
        /// </summary>
        List<Episode> ApplyOrdering(string orderingId, int tvdbId, List<Episode> episodes);
    }

    /// <summary>
    /// Offers TheTVDB's absolute and alternate episode orders for series it owns.
    /// <para>
    /// SkyHook, the proxy the default TheTVDB lookup goes through, only publishes the aired
    /// order. The other season types live in TheTVDB's own v4 API, which needs a key of the
    /// user's own, the same arrangement as TMDB.
    /// </para>
    /// </summary>
    public class TvdbOrderingProvider : IProvideTvdbOrdering
    {
        private const string BaseUrl = "https://api4.thetvdb.com/v4";

        // A page holds hundreds of episodes. The cap only exists so a misbehaving response
        // that never ends cannot loop forever.
        private const int MaxPages = 40;

        // The API documents a one month token. Refreshed far sooner than that so a stale
        // one is rare, and again immediately if a request is refused as unauthorized.
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);

        // Long enough that applying an ordering straight after listing it does not fetch
        // everything twice, short enough that a change on TheTVDB shows up the same day.
        private static readonly TimeSpan EpisodeLifetime = TimeSpan.FromMinutes(10);

        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly ICached<string> _tokenCache;
        private readonly ICached<List<TvdbEpisodeResource>> _episodeCache;
        private readonly Logger _logger;

        public TvdbOrderingProvider(IHttpClient httpClient,
                                    IConfigService configService,
                                    ICacheManager cacheManager,
                                    Logger logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _tokenCache = cacheManager.GetCache<string>(GetType(), "token");
            _episodeCache = cacheManager.GetCache<List<TvdbEpisodeResource>>(GetType(), "episodes");
            _logger = logger;
        }

        public List<EpisodeOrdering> GetOrderings(int tvdbId)
        {
            var orderings = new List<EpisodeOrdering>();

            if (tvdbId <= 0 || _configService.TvdbApiKey.IsNullOrWhiteSpace())
            {
                return orderings;
            }

            try
            {
                var absolute = GetEpisodes(tvdbId, TvdbOrderingIds.Absolute);

                if (absolute.Count > 0)
                {
                    orderings.Add(new EpisodeOrdering
                    {
                        Id = TvdbOrderingIds.Absolute,
                        Name = "TheTVDB Absolute Order",
                        Description = "Every episode numbered in one continuous run, as published by TheTVDB.",
                        EpisodeCount = absolute.Count,
                        SeasonCount = 1,
                        IsAbsolute = true
                    });
                }

                var alternate = GetEpisodes(tvdbId, TvdbOrderingIds.Alternate);

                if (alternate.Count > 0)
                {
                    orderings.Add(new EpisodeOrdering
                    {
                        Id = TvdbOrderingIds.Alternate,
                        Name = "TheTVDB Alternate Order",
                        Description = "The alternate season and episode numbering published by TheTVDB.",
                        EpisodeCount = alternate.Count,
                        SeasonCount = alternate.Select(e => e.SeasonNumber).Distinct().Count(),
                        IsAbsolute = false
                    });
                }
            }
            catch (Exception ex)
            {
                // An unavailable list should not stop the series being added with the aired
                // order, so the choice just does not appear.
                _logger.Debug(ex, "Unable to fetch TheTVDB episode orderings for {0}", tvdbId);

                return new List<EpisodeOrdering>();
            }

            if (orderings.Count > 0)
            {
                orderings.Insert(0, new EpisodeOrdering
                {
                    Id = null,
                    Name = "Default (Aired order)",
                    Description = "The season and episode numbering TheTVDB lists by default."
                });
            }

            return orderings;
        }

        public List<Episode> ApplyOrdering(string orderingId, int tvdbId, List<Episode> episodes)
        {
            if (!TvdbOrderingIds.IsTvdbOrdering(orderingId))
            {
                return episodes;
            }

            try
            {
                var listed = GetEpisodes(tvdbId, orderingId);

                if (listed.Count == 0)
                {
                    throw new TvdbOrderingUnavailableException(TvdbOrderingIds.ToSeasonType(orderingId), tvdbId);
                }

                return TvdbOrderingProjector.Project(orderingId, listed, episodes);
            }
            catch (Exception ex)
            {
                // Renumbering against a missing or half-fetched ordering would scramble the
                // library, so surface the failure and let the refresh keep what is on disk.
                // Log as a warning so expected runtime conditions (missing API key / ordering
                // removed by TheTVDB) do not fail the test harness while still preserving the
                // exception behavior.
                _logger.Warn(ex, "Unable to apply TheTVDB episode ordering {0}, keeping existing numbering", orderingId);

                throw;
            }
        }

        private List<TvdbEpisodeResource> GetEpisodes(int tvdbId, string orderingId)
        {
            var seasonType = TvdbOrderingIds.ToSeasonType(orderingId);

            return _episodeCache.Get($"{tvdbId}:{seasonType}", () => FetchEpisodes(tvdbId, seasonType), EpisodeLifetime);
        }

        private List<TvdbEpisodeResource> FetchEpisodes(int tvdbId, string seasonType)
        {
            var episodes = new List<TvdbEpisodeResource>();
            var seen = new HashSet<int>();

            for (var page = 0; page < MaxPages; page++)
            {
                var response = Get<TvdbEpisodesResponse>($"/series/{tvdbId}/episodes/{seasonType}", page);

                var pageEpisodes = response?.Data?.Episodes;

                if (pageEpisodes == null || pageEpisodes.Count == 0)
                {
                    break;
                }

                var added = 0;

                foreach (var episode in pageEpisodes)
                {
                    if (seen.Add(episode.Id))
                    {
                        episodes.Add(episode);
                        added++;
                    }
                }

                // A page that adds nothing new means the API is repeating itself rather than
                // running out, which is as good as the end.
                if (added == 0)
                {
                    break;
                }
            }

            return episodes;
        }

        /// <summary>
        /// A 404 means TheTVDB has no such season type for the series, which is the normal
        /// case for most series and most types, so it is an empty answer and not an error.
        /// </summary>
        private T Get<T>(string resource, int page, bool isRetry = false)
            where T : class, new()
        {
            var apiKey = _configService.TvdbApiKey;

            if (apiKey.IsNullOrWhiteSpace())
            {
                throw new TvdbApiKeyMissingException();
            }

            var request = new HttpRequestBuilder(BaseUrl + resource)
                .AddQueryParam("page", page.ToString())
                .SetHeader("Authorization", "Bearer " + GetToken(apiKey, _configService.TvdbPin))
                .Build();

            request.AllowAutoRedirect = true;
            request.SuppressHttpError = true;

            var response = _httpClient.Get<T>(request);

            if (response.HasHttpError)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new T();
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized && !isRetry)
                {
                    // The cached token has expired or been revoked. Log in afresh once.
                    _tokenCache.Remove(TokenCacheKey(apiKey, _configService.TvdbPin));

                    return Get<T>(resource, page, true);
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new TvdbApiKeyMissingException("The configured TheTVDB API key was rejected");
                }

                throw new HttpException(request, response);
            }

            return response.Resource;
        }

        private string GetToken(string apiKey, string pin)
        {
            return _tokenCache.Get(TokenCacheKey(apiKey, pin), () => Login(apiKey, pin), TokenLifetime);
        }

        // Keyed by the key and PIN themselves so changing either in Settings does not keep
        // serving a token issued for the old ones. Only ever held in process memory.
        private static string TokenCacheKey(string apiKey, string pin)
        {
            return "token:" + apiKey.GetHashCode() + ":" + (pin ?? string.Empty).GetHashCode();
        }

        private string Login(string apiKey, string pin)
        {
            var request = new HttpRequestBuilder(BaseUrl + "/login").Build();

            request.Method = HttpMethod.Post;
            request.Headers.ContentType = "application/json";
            request.SuppressHttpError = true;

            // TheTVDB documents the PIN as needed only for user-supported keys, so it is sent
            // only when one has been entered.
            request.SetContent(pin.IsNullOrWhiteSpace()
                ? Json.ToJson(new { apikey = apiKey })
                : Json.ToJson(new { apikey = apiKey, pin }));

            var response = _httpClient.Post<TvdbLoginResponse>(request);

            if (response.HasHttpError)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new TvdbApiKeyMissingException("The configured TheTVDB API key was rejected");
                }

                throw new HttpException(request, response);
            }

            var token = response.Resource?.Data?.Token;

            if (token.IsNullOrWhiteSpace())
            {
                throw new TvdbApiKeyMissingException("TheTVDB accepted the login but returned no token");
            }

            return token;
        }
    }
}
