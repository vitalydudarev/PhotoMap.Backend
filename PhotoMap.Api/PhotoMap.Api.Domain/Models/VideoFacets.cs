namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// What the videos a filter takes can still be narrowed down to, for each of the filters, with how many videos each
/// option takes: the videos the filter takes once that filter itself is left out, so that picking an option alone
/// takes as many videos as it counts.
/// </summary>
/// <param name="Folders">The folders the videos are in, without the file names.</param>
/// <param name="Years">The years, in UTC, the videos were taken in.</param>
/// <param name="Locations">The locations the videos were taken in, at every level, by their IDs.</param>
/// <param name="UnknownLocationCount">How many of the videos are in no location.</param>
/// <param name="Tags">The tags of the videos, by their IDs.</param>
/// <param name="UntaggedCount">How many of the videos have no tag.</param>
public record VideoFacets(
    IReadOnlyList<FacetCount<string>> Folders,
    IReadOnlyList<FacetCount<int>> Years,
    IReadOnlyList<FacetCount<long>> Locations,
    int UnknownLocationCount,
    IReadOnlyList<FacetCount<long>> Tags,
    int UntaggedCount);
