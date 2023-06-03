namespace PhotoMap.Api.Services;

public class DropboxDownloadState
{
    public long UserId { get; set; }
    public int TotalFiles { get; set; }
    public int? LastProcessedFileIndex { get; set; }
    public string? LastProcessedFileId { get; set; }
    public DateTimeOffset? LastAccessTime { get; set; }
}