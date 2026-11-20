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
/// <param name="Area">The box of coordinates the photos were taken within, sides included, or null for the photos
/// taken anywhere, and those with no GPS location. The photos with no GPS location are taken in no box.</param>
/// <param name="LocationIds">The locations the photos were taken in, any of them, see <see cref="Location"/>; the
/// photos not geocoded are in none. With <paramref name="UnknownLocation"/>, the photos in no location are taken too.
/// The photos taken anywhere, and those with no location, when neither is given.</param>
/// <param name="UnknownLocation">Whether the photos in no location are taken, along with those of
/// <paramref name="LocationIds"/>: those with no GPS, not geocoded yet, or where the geocoding found nothing. Only they
/// are when no location is given.</param>
/// <param name="FolderPaths">The folders the photos are in, any of them, without the file names, such as
/// <c>disk:/Camera Uploads</c>; the photos of every folder when not given.</param>
public record PhotoFilter(
    IReadOnlyCollection<long> PhotoSourceIds,
    IReadOnlyCollection<int> Years,
    IReadOnlyCollection<PhotoCategory> Categories,
    bool? HasGps = null,
    bool IncludeOtherUploads = false,
    IReadOnlyCollection<long>? TagIds = null,
    bool Untagged = false,
    GeoBox? Area = null,
    IReadOnlyCollection<long>? LocationIds = null,
    bool UnknownLocation = false,
    IReadOnlyCollection<string>? FolderPaths = null)
{
    public static PhotoFilter All { get; } = new([], [], []);

    /// <summary>
    /// Whether the photos are narrowed down by their tags.
    /// </summary>
    public bool FiltersByTags => (TagIds?.Count ?? 0) > 0 || Untagged;

    /// <summary>
    /// Whether the photos are narrowed down by the locations they were taken in.
    /// </summary>
    public bool FiltersByLocations => (LocationIds?.Count ?? 0) > 0 || UnknownLocation;
}
