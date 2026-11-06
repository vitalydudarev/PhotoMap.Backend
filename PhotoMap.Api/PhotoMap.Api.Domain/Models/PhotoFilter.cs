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
/// <param name="IncludeOtherUploads">Whether the photos outside the folders the sources are processed from are taken
/// too, see <see cref="Photo.FromOtherUploads"/>.</param>
/// <param name="TagIds">The tags the photos have, any of them. With <paramref name="Untagged"/>, the photos with no tag
/// are taken too.</param>
/// <param name="Untagged">Whether the photos with no tag are taken, along with those of <paramref name="TagIds"/>;
/// only they are when no tag is given.</param>
public record PhotoFilter(
    IReadOnlyCollection<long> PhotoSourceIds,
    IReadOnlyCollection<int> Years,
    IReadOnlyCollection<PhotoCategory> Categories,
    bool? HasGps = null,
    bool IncludeOtherUploads = false,
    IReadOnlyCollection<long>? TagIds = null,
    bool Untagged = false)
{
    public static PhotoFilter All { get; } = new([], [], []);

    /// <summary>
    /// Whether the photos are narrowed down by their tags.
    /// </summary>
    public bool FiltersByTags => (TagIds?.Count ?? 0) > 0 || Untagged;
}
