namespace PhotoMap.Shared.Models;

public class ProcessedImage
{
    public string FileName { get; set; } = null!;
    public string FileUrl { get; set; }
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    public string PhotoSourceName { get; set; } = null!;
    public string RelativeFilePath { get; set; }
    public string Path { get; set; }
    public string? ExternalId { get; set; }
    public string ContentHash { get; set; } = null!;
    public DateTime? FileCreatedOn { get; set; }
    public Dictionary<int, byte[]> Thumbs { get; set; } = null!;
    public DateTime? PhotoTakenOn { get; set; }
    public string? ExifString { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
