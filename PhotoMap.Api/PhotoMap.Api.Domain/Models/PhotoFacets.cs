namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// What the photos a filter takes can still be narrowed down to, for each of the filters: the values of the photos the
/// filter takes once that filter itself is left out, so that picking one of them takes some photo. The copies of a
/// photo are all counted, as picking one copy's source or folder takes that copy.
/// </summary>
/// <param name="PhotoSourceIds">The photo sources of the photos.</param>
/// <param name="FolderPaths">The folders the photos are in, without the file names, by name.</param>
/// <param name="Years">The years, in UTC, the photos were taken in, oldest first.</param>
/// <param name="Categories">The categories of the photos, <see cref="PhotoCategory.Other"/> for those in none; never
/// <see cref="PhotoCategory.Deleted"/>, as the deleted photos are listed apart.</param>
/// <param name="Gps">Whether the photos have a GPS location: true, false, or both.</param>
/// <param name="TagIds">The tags of the photos.</param>
/// <param name="Untagged">Whether some of the photos have no tag.</param>
/// <param name="LocationIds">The locations the photos were taken in, at every level.</param>
/// <param name="UnknownLocation">Whether some of the photos are in no location.</param>
public record PhotoFacets(
    IReadOnlyList<long> PhotoSourceIds,
    IReadOnlyList<string> FolderPaths,
    IReadOnlyList<int> Years,
    IReadOnlyList<PhotoCategory> Categories,
    IReadOnlyList<bool> Gps,
    IReadOnlyList<long> TagIds,
    bool Untagged,
    IReadOnlyList<long> LocationIds,
    bool UnknownLocation);
