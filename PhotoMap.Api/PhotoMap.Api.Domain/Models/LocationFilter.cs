namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// The locations the photos or videos are taken from, as the reverse geocoding of their GPS locations tells.
/// </summary>
/// <param name="LocationIds">The locations they were taken in, any of them, see <see cref="Location"/>; those not
/// geocoded are in none. With <paramref name="UnknownLocation"/>, those in no location are taken too. Those taken
/// anywhere, and those with no location, when neither is given.</param>
/// <param name="UnknownLocation">Whether those in no location are taken, along with those of
/// <paramref name="LocationIds"/>: those with no GPS, not geocoded yet, or where the geocoding found nothing. Only they
/// are when no location is given.</param>
public record LocationFilter(IReadOnlyCollection<long> LocationIds, bool UnknownLocation = false)
{
    public static LocationFilter All { get; } = new([]);

    /// <summary>
    /// Whether they are narrowed down by the locations they were taken in.
    /// </summary>
    public bool FiltersByLocations => LocationIds.Count > 0 || UnknownLocation;
}
