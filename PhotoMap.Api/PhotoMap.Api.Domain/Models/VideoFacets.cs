namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// What the videos a filter takes can still be narrowed down to, for each of the filters: the values of the videos the
/// filter takes once that filter itself is left out, so that picking one of them takes some video.
/// </summary>
/// <param name="FolderPaths">The folders the videos are in, without the file names, by name.</param>
/// <param name="Years">The years, in UTC, the videos were taken in, oldest first.</param>
/// <param name="LocationIds">The locations the videos were taken in, at every level.</param>
/// <param name="UnknownLocation">Whether some of the videos are in no location.</param>
public record VideoFacets(
    IReadOnlyList<string> FolderPaths,
    IReadOnlyList<int> Years,
    IReadOnlyList<long> LocationIds,
    bool UnknownLocation);
