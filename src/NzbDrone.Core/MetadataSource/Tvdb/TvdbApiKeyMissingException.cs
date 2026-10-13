using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.MetadataSource.Tvdb
{
    public class TvdbApiKeyMissingException : NzbDroneException
    {
        public TvdbApiKeyMissingException()
            : base("No TheTVDB API key is configured. Add one in Settings to use TheTVDB's absolute and alternate episode orders.")
        {
        }

        public TvdbApiKeyMissingException(string message)
            : base(message)
        {
        }
    }
}
