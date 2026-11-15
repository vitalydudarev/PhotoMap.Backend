namespace PhotoMap.Api.Services.Services;

public interface IGeocoder
{
    /// <summary>
    /// Whether the geocoder can be used at all, e.g. it has the access token it needs.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Finds the address of the location.
    /// </summary>
    /// <returns>The response of the geocoding API, JSON, as is.</returns>
    /// <exception cref="PhotoMap.Api.Services.Exceptions.GeocodingUnavailableException">No location can be geocoded
    /// for now.</exception>
    /// <exception cref="HttpRequestException">This location could not be geocoded.</exception>
    Task<string> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken);
}
