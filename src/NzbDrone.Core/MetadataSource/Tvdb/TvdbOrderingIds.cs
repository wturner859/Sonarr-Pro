using System;

namespace NzbDrone.Core.MetadataSource.Tvdb
{
    /// <summary>
    /// The ordering ids a TheTVDB-owned series can be added with, and the TheTVDB season
    /// type each one stands for.
    /// <para>
    /// The aired order is not listed: it is the default, stored as no ordering at all, and
    /// is what SkyHook returns without any renumbering.
    /// </para>
    /// <para>
    /// The ids carry a prefix so they can never be mistaken for a TMDB episode group id
    /// (an opaque hex string) or the IMDb ordering id.
    /// </para>
    /// </summary>
    public static class TvdbOrderingIds
    {
        public const string Absolute = "tvdb-absolute";
        public const string Alternate = "tvdb-alternate";

        public static bool IsTvdbOrdering(string orderingId)
        {
            return ToSeasonType(orderingId) != null;
        }

        public static bool IsAbsolute(string orderingId)
        {
            return string.Equals(orderingId, Absolute, StringComparison.Ordinal);
        }

        /// <summary>
        /// The value TheTVDB's v4 API expects as the <c>season-type</c> path segment, or null
        /// when the id is not one of ours.
        /// </summary>
        public static string ToSeasonType(string orderingId)
        {
            if (string.Equals(orderingId, Absolute, StringComparison.Ordinal))
            {
                return "absolute";
            }

            if (string.Equals(orderingId, Alternate, StringComparison.Ordinal))
            {
                return "alternate";
            }

            return null;
        }
    }
}
