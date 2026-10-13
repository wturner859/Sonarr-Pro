using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.MetadataSource.Tvdb.Resource;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Tvdb
{
    /// <summary>
    /// Renumbers episodes according to one of TheTVDB's alternative season types.
    /// <para>
    /// Kept free of HTTP and state so the renumbering can be tested directly: it decides
    /// what ends up in file names, and getting it wrong misfiles a library.
    /// </para>
    /// </summary>
    public static class TvdbOrderingProjector
    {
        /// <summary>
        /// Lays an ordering over episodes matched by TheTVDB episode id.
        /// <para>
        /// An absolute ordering is one continuous run, so every listed episode goes into
        /// season 1 numbered by its absolute number. Any other ordering takes the season and
        /// episode numbers TheTVDB lists for it.
        /// </para>
        /// <para>
        /// Episodes the ordering does not list are never dropped, since that would orphan any
        /// file already matched to them. They keep their existing numbering, unless another
        /// episode now holds that slot, in which case they move to the end of season 0. Two
        /// episodes sharing a season and episode number would otherwise break matching and
        /// the next refresh.
        /// </para>
        /// The input list's order is preserved.
        /// </summary>
        public static List<Episode> Project(string orderingId, IEnumerable<TvdbEpisodeResource> listed, List<Episode> episodes)
        {
            var isAbsolute = TvdbOrderingIds.IsAbsolute(orderingId);

            var byTvdbId = new Dictionary<int, Episode>();

            foreach (var episode in episodes)
            {
                if (episode.TvdbId > 0)
                {
                    byTvdbId.TryAdd(episode.TvdbId, episode);
                }
            }

            var claimed = new HashSet<Episode>();
            var taken = new HashSet<(int Season, int Number)>();

            foreach (var entry in (listed ?? Enumerable.Empty<TvdbEpisodeResource>())
                         .OrderBy(e => isAbsolute ? 1 : e.SeasonNumber)
                         .ThenBy(e => isAbsolute ? e.AbsoluteNumber ?? e.Number : e.Number))
            {
                if (!byTvdbId.TryGetValue(entry.Id, out var episode) || claimed.Contains(episode))
                {
                    continue;
                }

                var season = isAbsolute ? 1 : entry.SeasonNumber;
                var number = isAbsolute ? entry.AbsoluteNumber ?? entry.Number : entry.Number;

                // TheTVDB lists some episodes in an alternative ordering without a usable
                // number. Leaving those with their own numbering is safer than inventing one,
                // and a slot already held by another listed episode is not shared.
                if (number <= 0 || !taken.Add((season, number)))
                {
                    continue;
                }

                episode.SeasonNumber = season;
                episode.EpisodeNumber = number;

                if (isAbsolute)
                {
                    episode.AbsoluteEpisodeNumber = number;
                }

                claimed.Add(episode);
            }

            // Two passes over the leftovers. First, every one that can keep its own slot does,
            // so a real special is never displaced by a collision elsewhere. Only then are the
            // colliding ones moved, to numbers above everything already in season 0.
            var moved = new List<Episode>();

            foreach (var episode in episodes.Where(e => !claimed.Contains(e)))
            {
                if (!taken.Add((episode.SeasonNumber, episode.EpisodeNumber)))
                {
                    moved.Add(episode);
                }
            }

            var nextSpecial = taken.Where(t => t.Season == 0).Select(t => t.Number).DefaultIfEmpty(0).Max() + 1;

            foreach (var episode in moved)
            {
                episode.SeasonNumber = 0;
                episode.EpisodeNumber = nextSpecial++;
            }

            return episodes;
        }
    }
}
