namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// The locations the videos are taken from, as the gallery filters the photos by theirs.
/// </summary>
/// <param name="LocationIds">The locations the videos were taken in, any of them, see <see cref="Location"/>; the
/// videos not geocoded are in none. With <paramref name="UnknownLocation"/>, the videos in no location are taken too.
/// The videos taken anywhere, and those with no location, when neither is given.</param>
/// <param name="UnknownLocation">Whether the videos in no location are taken, along with those of
/// <paramref name="LocationIds"/>: those with no GPS, not geocoded yet, or where the geocoding found nothing. Only they
/// are when no location is given.</param>
public record VideoLocationFilter(IReadOnlyCollection<long> LocationIds, bool UnknownLocation = false)
{
    public static VideoLocationFilter All { get; } = new([]);

    /// <summary>
    /// Whether the videos are narrowed down by the locations they were taken in.
    /// </summary>
    public bool FiltersByLocations => LocationIds.Count > 0 || UnknownLocation;
}
