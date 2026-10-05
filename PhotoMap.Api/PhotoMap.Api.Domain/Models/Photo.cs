namespace PhotoMap.Api.Domain.Models;

public class Photo
{
    public long Id { get; set; }
    public required long UserId { get; set; }
    public string? ThumbnailSmallFilePath { get; set; }
    public string? ThumbnailLargeFilePath { get; set; }
    public required string FileName { get; set; }
    public DateTimeOffset DateTimeTaken { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool HasGps { get; set; }
    public string? ExifString { get; set; }
    public string? Path { get; set; }
    public string? ExternalId { get; set; }
    public string? ContentHash { get; set; }
    public DateTimeOffset AddedOn { get; set; }
    public long PhotoSourceId { get; set; }

    /// <summary>
    /// When the user marked the photo as deleted, null while it is not.
    /// </summary>
    public DateTimeOffset? DeletedOn { get; set; }

    /// <summary>
    /// The group of the photos of the user this one is a copy of, the ID of the first of them; null when it has none,
    /// or has not been looked at yet. See <c>PhotoDuplicateFinder</c>.
    /// </summary>
    public long? DuplicateGroupId { get; set; }
}
