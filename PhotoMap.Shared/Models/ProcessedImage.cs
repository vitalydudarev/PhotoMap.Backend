namespace PhotoMap.Shared.Models;

public class ProcessedImage
{
    public string FileName { get; set; } = null!;
    public string FileUrl { get; set; }
    // public long PhotoSourceId { get; set; }
    // public string FileSource { get; set; }
    public string RelativeFilePath { get; set; }
    public string Path { get; set; }
    public DateTime? FileCreatedOn { get; set; }
    public Dictionary<int, byte[]> Thumbs { get; set; } = null!;
    public DateTime? PhotoTakenOn { get; set; }
    public string? ExifString { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}