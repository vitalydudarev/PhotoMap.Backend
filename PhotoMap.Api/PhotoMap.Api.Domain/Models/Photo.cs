namespace PhotoMap.Api.Domain.Models;

public class Photo
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string ThumbnailSmallFilePath { get; set; }
    public string ThumbnailLargeFilePath { get; set; }
    public string FileName { get; set; }
    public DateTimeOffset DateTimeTaken { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool HasGps { get; set; }
    public string ExifString { get; set; }
    public string Source { get; set; }
    public string Path { get; set; }
    public DateTimeOffset AddedOn { get; set; }
}
