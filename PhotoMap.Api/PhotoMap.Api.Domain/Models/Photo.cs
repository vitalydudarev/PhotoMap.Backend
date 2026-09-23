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
}
