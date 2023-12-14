namespace PhotoMap.Api.Services;

public class DropboxDownloadState
{
    public int LastProcessedFileIndex { get; set; }
    public string LastProcessedFileId { get; set; } = null!;
}