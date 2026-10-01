namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// Which of the photos of a user to take. An empty list does not narrow the photos down: they come from all the
/// photo sources, from all the years, or are of every category. The photos marked as deleted are taken only when
/// <see cref="PhotoCategory.Deleted"/> is asked for.
/// </summary>
/// <param name="PhotoSourceIds">The photo sources to take the photos from.</param>
/// <param name="Years">The years, in UTC, the photos were taken in.</param>
/// <param name="Categories">The categories the photos are in, any of them. <see cref="PhotoCategory.Other"/> takes
/// the photos in none of the categories, <see cref="PhotoCategory.Deleted"/> the photos marked as deleted.</param>
/// <param name="HasGps">Whether the photos have a GPS location, or null for the photos with and without one.</param>
public record PhotoFilter(
    IReadOnlyCollection<long> PhotoSourceIds,
    IReadOnlyCollection<int> Years,
    IReadOnlyCollection<PhotoCategory> Categories,
    bool? HasGps = null)
{
    public static PhotoFilter All { get; } = new([], [], []);
}
