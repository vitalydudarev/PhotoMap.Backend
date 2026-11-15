namespace PhotoMap.Api.Services;

/// <summary>
/// The settings of the Mapbox geocoding, the "Mapbox" section. The access token is kept out of the appsettings: in the
/// user secrets in development, in the environment (Mapbox__AccessToken) elsewhere.
/// </summary>
public class MapboxSettings
{
    public const string SectionName = "Mapbox";

    /// <summary>
    /// The access token of the Mapbox account; without it nothing is geocoded.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// How many requests the geocoding sends a minute at most, the rate limit of the Mapbox Geocoding API (1000 by
    /// default).
    /// </summary>
    public int RequestsPerMinute { get; set; } = 1000;

    /// <summary>
    /// How many requests are on their way at a time. The rate limit decides how many are sent a minute; this only lets
    /// the slow responses not hold the rest back.
    /// </summary>
    public int MaxParallelRequests { get; set; } = 8;
}
