using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using Sonarr.Http.REST;

namespace Sonarr.Api.V5.Settings;

public class MetadataSourceSettingsResource : RestResource
{
    /// <summary>
    /// Stands in for a key that is already stored. The real key is never sent to the
    /// browser once saved; sending this value back means "leave it as it is".
    /// </summary>
    public const string Mask = "********";

    /// <summary>
    /// User-supplied TMDB API key. TMDB issues one per account and does not permit a
    /// shared key to be redistributed, so there is no default.
    /// </summary>
    public string? TmdbApiKey { get; set; }

    /// <summary>
    /// Whether a key is stored, since the key itself comes back masked and the interface
    /// cannot tell the difference from a blank one.
    /// </summary>
    public bool TmdbApiKeyConfigured { get; set; }

    /// <summary>
    /// User-supplied TheTVDB v4 API key, needed only for TheTVDB's absolute and alternate
    /// episode orders. SkyHook serves everything else without one. Masked once saved, like
    /// the TMDB key.
    /// </summary>
    public string? TvdbApiKey { get; set; }

    public bool TvdbApiKeyConfigured { get; set; }

    /// <summary>
    /// The PIN TheTVDB issues with a user-supported key. Blank for a project key. Masked once
    /// saved.
    /// </summary>
    public string? TvdbPin { get; set; }

    public bool TvdbPinConfigured { get; set; }
}

public static class MetadataSourceSettingsResourceMapper
{
    public static MetadataSourceSettingsResource ToResource(IConfigService model)
    {
        var configured = model.TmdbApiKey.IsNotNullOrWhiteSpace();
        var tvdbConfigured = model.TvdbApiKey.IsNotNullOrWhiteSpace();
        var pinConfigured = model.TvdbPin.IsNotNullOrWhiteSpace();

        return new MetadataSourceSettingsResource
        {
            TmdbApiKey = configured ? MetadataSourceSettingsResource.Mask : string.Empty,
            TmdbApiKeyConfigured = configured,
            TvdbApiKey = tvdbConfigured ? MetadataSourceSettingsResource.Mask : string.Empty,
            TvdbApiKeyConfigured = tvdbConfigured,
            TvdbPin = pinConfigured ? MetadataSourceSettingsResource.Mask : string.Empty,
            TvdbPinConfigured = pinConfigured
        };
    }
}
