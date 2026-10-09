namespace PhotoMap.Api.DTOs;

/// <summary>
/// The status of the last download of a photo source, its file counters, what it downloads and where to.
/// </summary>
public class SourceDownloadStatusDto : PhotoSourceProgressDto
{
    /// <summary>
    /// What the last download has taken, null when the source has not been downloaded.
    /// </summary>
    public SourceDownloadContentDto? Content { get; set; }

    /// <summary>
    /// The last download has taken only the files that failed before, rather than the folder.
    /// </summary>
    public bool RetryingFailed { get; set; }

    /// <summary>
    /// The folder the last chosen folder download has taken its files from, null for any other download.
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// The files found downloaded whole when the last download started, which it has skipped.
    /// </summary>
    public int AlreadyDownloadedCount { get; set; }

    /// <summary>
    /// The files that failed to download, over all downloads, and are not downloaded yet.
    /// </summary>
    public int FailedFileCount { get; set; }

    /// <summary>
    /// The folder on the disk of the backend the files are downloaded to.
    /// </summary>
    public string TargetFolder { get; set; } = null!;
}
