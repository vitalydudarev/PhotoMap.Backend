namespace PhotoMap.Api.DTOs;

/// <summary>
/// A file of a photo source the last download has found missing from the file storage, with what has become of it.
/// </summary>
public class MissingDownloadDto
{
    /// <summary>
    /// The folder of the source the file is in, empty for the folder the photos are processed from.
    /// </summary>
    public string Folder { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public long Size { get; set; }
    public SourceDownloadContentDto Content { get; set; }
    /// <summary>
    /// Pending, while the download has not got to the file or has stopped before it did, Downloaded or Failed.
    /// </summary>
    public string Status { get; set; } = null!;
    /// <summary>
    /// What the file has failed with, absent unless it has failed.
    /// </summary>
    public string? Error { get; set; }
}
