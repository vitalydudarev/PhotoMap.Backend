namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// A tag of the photos and the videos of a user, which the gallery and the videos are filtered by; a tag is the same
/// for both.
/// </summary>
/// <param name="Id">The ID of the tag.</param>
/// <param name="Name">The name of the tag, as the user first wrote it.</param>
/// <param name="PhotoCount">How many photos have the tag, the deleted ones too.</param>
/// <param name="VideoCount">How many videos have the tag.</param>
public record PhotoTag(long Id, string Name, int PhotoCount, int VideoCount = 0);
