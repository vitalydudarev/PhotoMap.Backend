namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// The result of the reverse geocoding of a photo.
/// </summary>
/// <param name="Response">The response of the geocoding API as it is.</param>
/// <param name="Locations">The locations the response tells the photo was taken in, its country, its city and so on;
/// see <c>GeocodedLocations</c>.</param>
public record PhotoGeocoding(string Response, IReadOnlyCollection<GeocodedLocation> Locations);
