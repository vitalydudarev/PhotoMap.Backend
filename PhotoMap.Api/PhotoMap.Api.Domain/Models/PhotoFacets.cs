namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// What the photos a filter takes can still be narrowed down to, for each of the filters, with how many photos each
/// option takes: the photos the filter takes once that filter itself is left out, so that picking an option alone
/// takes as many photos as it counts. The copies of a photo count once, as the photos are listed, but each copy counts
/// for its own source, folder and so on, as picking one copy's source or folder takes that copy.
/// </summary>
/// <param name="PhotoSources">The photo sources of the photos, by their IDs.</param>
/// <param name="Folders">The folders the photos are in, without the file names.</param>
/// <param name="Years">The years, in UTC, the photos were taken in.</param>
/// <param name="Categories">The categories of the photos, <see cref="PhotoCategory.Other"/> for those in none; never
/// <see cref="PhotoCategory.Deleted"/>, as the deleted photos are listed apart.</param>
/// <param name="Gps">Whether the photos have a GPS location.</param>
/// <param name="Tags">The tags of the photos, by their IDs.</param>
/// <param name="UntaggedCount">How many of the photos have no tag.</param>
/// <param name="Locations">The locations the photos were taken in, at every level, by their IDs.</param>
/// <param name="UnknownLocationCount">How many of the photos are in no location.</param>
public record PhotoFacets(
    IReadOnlyList<FacetCount<long>> PhotoSources,
    IReadOnlyList<FacetCount<string>> Folders,
    IReadOnlyList<FacetCount<int>> Years,
    IReadOnlyList<FacetCount<PhotoCategory>> Categories,
    IReadOnlyList<FacetCount<bool>> Gps,
    IReadOnlyList<FacetCount<long>> Tags,
    int UntaggedCount,
    IReadOnlyList<FacetCount<long>> Locations,
    int UnknownLocationCount);
