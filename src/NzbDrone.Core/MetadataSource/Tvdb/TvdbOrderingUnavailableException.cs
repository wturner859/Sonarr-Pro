using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.MetadataSource.Tvdb
{
    /// <summary>
    /// A series was added with one of TheTVDB's alternative orders, but TheTVDB no longer
    /// lists that order. Raised instead of falling back to the aired order, because the
    /// fallback would renumber the series and so rename its files.
    /// </summary>
    public class TvdbOrderingUnavailableException : NzbDroneException
    {
        public TvdbOrderingUnavailableException(string seasonType, int tvdbId)
            : base("TheTVDB no longer lists a {0} episode order for series {1}", seasonType, tvdbId)
        {
        }
    }
}
