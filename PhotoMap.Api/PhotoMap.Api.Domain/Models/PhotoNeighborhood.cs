namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// The photos taken around the time or the place a photo was, to suggest tags for it by: the photos taken between
/// <paramref name="From"/> and <paramref name="To"/>, and those taken within the box of coordinates, if any.
/// </summary>
public record PhotoNeighborhood(
    DateTimeOffset From,
    DateTimeOffset To,
    GeoBox? Box);

/// <summary>
/// A box of coordinates, in degrees.
/// </summary>
public record GeoBox(double MinLatitude, double MaxLatitude, double MinLongitude, double MaxLongitude);
