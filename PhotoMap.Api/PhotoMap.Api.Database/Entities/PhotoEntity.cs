namespace PhotoMap.Api.Database.Entities;

public class PhotoEntity
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public UserEntity? User { get; set; }
    public long PhotoSourceId { get; set; }
    public PhotoSourceEntity? PhotoSource { get; set; }
    public string? ThumbnailSmallFilePath { get; set; }
    public string? ThumbnailLargeFilePath { get; set; }
    public required string FileName { get; set; }
    public required DateTimeOffset DateTimeTaken { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool HasGps { get; set; }
    public string? ExifString { get; set; }
    public string? Path { get; set; }
    public required DateTimeOffset AddedOn { get; set; }
}