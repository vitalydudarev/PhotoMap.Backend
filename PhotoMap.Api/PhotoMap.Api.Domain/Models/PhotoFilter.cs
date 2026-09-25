namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// Which of the photos of a user to take. An empty list does not narrow the photos down: they come from all the
/// photo sources, or from all the years.
/// </summary>
/// <param name="PhotoSourceIds">The photo sources to take the photos from.</param>
/// <param name="Years">The years, in UTC, the photos were taken in.</param>
public record PhotoFilter(IReadOnlyCollection<long> PhotoSourceIds, IReadOnlyCollection<int> Years)
{
    public static PhotoFilter All { get; } = new([], []);
}
