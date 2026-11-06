namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// What a tag is suggested for a photo by: when and where a photo with tags was taken, and its tags.
/// </summary>
public record TaggedPhoto(
    long Id,
    DateTimeOffset DateTimeTaken,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<string> Tags);
