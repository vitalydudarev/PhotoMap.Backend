namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// A file of a photo source that could not be downloaded into the file storage, kept until a later download gets it
/// whole.
/// </summary>
public class FailedDownload
{
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    public SourceDownloadKind Kind { get; set; }
    /// <summary>
    /// The folder of the source the file is in, empty for the folder the photos are processed from.
    /// </summary>
    public string Folder { get; set; } = "";
    /// <summary>
    /// The name of the file, which is unique in its folder.
    /// </summary>
    public required string FileName { get; set; }
    /// <summary>
    /// What the source downloads the file by (Yandex.Disk path, Dropbox file ID).
    /// </summary>
    public required string Path { get; set; }
    public long Size { get; set; }
    public SourceDownloadContent Content { get; set; }
    public required string Error { get; set; }
    /// <summary>
    /// How many times the file has failed, over all downloads.
    /// </summary>
    public int Attempts { get; set; }
    public DateTimeOffset FailedAt { get; set; }
}
