namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// What a tag is suggested for a photo or a video by: when and where a photo, or a video, with tags was taken, and its
/// tags, which are the same for both.
/// </summary>
/// <param name="IsVideo">Whether it is a video, whose IDs are apart from those of the photos.</param>
public record TaggedPhoto(
    long Id,
    DateTimeOffset DateTimeTaken,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<string> Tags,
    bool IsVideo = false);
