namespace PhotoMap.Api.Database.Entities;

public class VideoEntity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public UserEntity? User { get; set; }
    public long PhotoSourceId { get; set; }
    public PhotoSourceEntity? PhotoSource { get; set; }
    public required string ExternalId { get; set; }
    public required string FileName { get; set; }
    public string? FolderPath { get; set; }
    public string? MimeType { get; set; }
    public long Size { get; set; }
    public required DateTimeOffset DateTimeTaken { get; set; }
    public DateTimeOffset? ExifDateTime { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? PreviewFilePath { get; set; }
    public string? PreviewContentType { get; set; }
    public required DateTimeOffset AddedOn { get; set; }
}
