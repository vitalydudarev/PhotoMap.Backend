namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// Which of the photos of a user to take. An empty list does not narrow the photos down: they come from all the
/// photo sources, from all the years, or are of every category.
/// </summary>
/// <param name="PhotoSourceIds">The photo sources to take the photos from.</param>
/// <param name="Years">The years, in UTC, the photos were taken in.</param>
/// <param name="Categories">The categories the photos are in, any of them. <see cref="PhotoCategory.Other"/> takes
/// the photos in none of the categories.</param>
public record PhotoFilter(
    IReadOnlyCollection<long> PhotoSourceIds,
    IReadOnlyCollection<int> Years,
    IReadOnlyCollection<PhotoCategory> Categories)
{
    public static PhotoFilter All { get; } = new([], [], []);
}
