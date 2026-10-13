using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.MetadataSource.Imdb;
using NzbDrone.Core.MetadataSource.Tmdb;
using NzbDrone.Core.MetadataSource.Tvdb;
using Sonarr.Http;

namespace Sonarr.Api.V5.Series;

/// <summary>
/// Lists the episode orderings a series can be added with.
/// <para>
/// An ordering is picked when a series is added and fixed thereafter, because season and
/// episode numbers are written into file and folder names.
/// </para>
/// </summary>
[V5ApiController("series/ordering")]
public class EpisodeOrderingController : Controller
{
    private readonly IProvideEpisodeOrderings _orderingProvider;
    private readonly IProvideImdbOrdering _imdbOrdering;
    private readonly IProvideTvdbOrdering _tvdbOrdering;

    public EpisodeOrderingController(IProvideEpisodeOrderings orderingProvider, IProvideImdbOrdering imdbOrdering, IProvideTvdbOrdering tvdbOrdering)
    {
        _orderingProvider = orderingProvider;
        _imdbOrdering = imdbOrdering;
        _tvdbOrdering = tvdbOrdering;
    }

    [HttpGet]
    public Ok<List<EpisodeOrderingResource>> GetOrderings([FromQuery] int tmdbId, [FromQuery] string? imdbId = null, [FromQuery] int tvdbId = 0)
    {
        var orderings = new List<EpisodeOrderingResource>();

        if (tmdbId > 0)
        {
            // GetOrderings always yields the default ordering and swallows lookup failures,
            // so a missing or rejected API key degrades to "default only" rather than
            // erroring. The caller shows no choice when that is all it gets.
            orderings.AddRange(_orderingProvider.GetOrderings(tmdbId.ToString()).ToResource());
        }

        // Null unless the local IMDb index has been built and holds this series, so the
        // option only appears once it can actually be used.
        var imdb = _imdbOrdering.GetOrdering(imdbId);

        if (imdb != null)
        {
            orderings.Add(imdb.ToResource());
        }

        // Only asked about for a series TheTVDB owns, since these orderings are applied by the
        // TheTVDB provider and would do nothing for a series owned by another one. Empty
        // without a TheTVDB API key, or when TheTVDB lists nothing beyond the aired order.
        // Its own default entry is dropped when another source already supplied one.
        if (tvdbId > 0)
        {
            foreach (var ordering in _tvdbOrdering.GetOrderings(tvdbId))
            {
                if (ordering.IsDefault && orderings.Any(o => o.IsDefault))
                {
                    continue;
                }

                orderings.Add(ordering.ToResource());
            }
        }

        return TypedResults.Ok(orderings);
    }
}
