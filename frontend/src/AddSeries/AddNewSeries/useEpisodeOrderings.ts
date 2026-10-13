import useApiQuery from 'Helpers/Hooks/useApiQuery';

export interface EpisodeOrdering {
  id: string | null;
  name: string;
  description: string;
  episodeCount: number;
  seasonCount: number;
  isAbsolute: boolean;
  isDefault: boolean;
}

const DEFAULT_ORDERINGS: EpisodeOrdering[] = [];

/**
 * Episode orderings published for a series: TMDB episode groups, IMDb's own
 * numbering once the local IMDb index has been built, and TheTVDB's absolute and
 * alternate orders once a TheTVDB API key is saved.
 *
 * The ids are sent because they are answered by different sources. A series known
 * to IMDb but not TMDB still has an ordering to offer, and asking only about TMDB
 * would hide it.
 *
 * Pass a TheTVDB id only for a series TheTVDB owns: those orderings are applied by
 * the TheTVDB provider, so offering them for a series owned by another source would
 * be a choice that does nothing.
 *
 * An empty list means the series is added with the provider's default ordering,
 * which is the normal case for a series with nothing published and for an install
 * with no keys saved.
 */
export const useEpisodeOrderings = (
  tmdbId: number,
  imdbId?: string,
  tvdbId = 0,
  isEnabled = true
) => {
  const result = useApiQuery<EpisodeOrdering[]>({
    path: '/series/ordering',
    queryParams: {
      tmdbId,
      imdbId,
      tvdbId,
    },
    queryOptions: {
      enabled: isEnabled && (tmdbId > 0 || !!imdbId || tvdbId > 0),
      refetchOnWindowFocus: false,
    },
  });

  return {
    ...result,
    data: result.data ?? DEFAULT_ORDERINGS,
  };
};
