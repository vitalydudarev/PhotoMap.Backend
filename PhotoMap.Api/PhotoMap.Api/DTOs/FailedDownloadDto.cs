namespace PhotoMap.Api.DTOs;

/// <summary>
/// A file of a photo source that failed to download and is not downloaded yet.
/// </summary>
public class FailedDownloadDto
{
    /// <summary>
    /// The folder of the source the file is in, empty for the folder the photos are processed from.
    /// </summary>
    public string Folder { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public long Size { get; set; }
    public SourceDownloadContentDto Content { get; set; }
    public string Error { get; set; } = null!;
    /// <summary>
    /// How many times the file has failed, over all downloads.
    /// </summary>
    public int Attempts { get; set; }
    public string FailedAt { get; set; } = null!;
}
