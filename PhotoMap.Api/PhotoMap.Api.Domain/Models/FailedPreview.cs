namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// Which record of failed files a failed file comes from.
/// </summary>
public enum FailedPreviewTable
{
    /// <summary>
    /// A file the processing of the photos could not download or process.
    /// </summary>
    FailedFiles = 1,

    /// <summary>
    /// A file a download into the file storage could not download.
    /// </summary>
    FailedDownloads = 2
}

/// <summary>
/// A file of a photo source that failed, with the preview downloaded of it to show which file it is. The previews are
/// the largest the source makes: XXXL on Yandex.Disk, a smaller one when it has none, and 2048×1536 on Dropbox.
/// </summary>
public class FailedPreview
{
    public FailedPreviewTable Table { get; set; }
    /// <summary>
    /// The ID of the failed file in its table.
    /// </summary>
    public long Id { get; set; }
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    /// <summary>
    /// What the processing of the photos failed at, for a file of <see cref="FailedPreviewTable.FailedFiles"/>.
    /// </summary>
    public FileFailureStage? Stage { get; set; }
    /// <summary>
    /// Which download failed, for a file of <see cref="FailedPreviewTable.FailedDownloads"/>.
    /// </summary>
    public SourceDownloadKind? DownloadKind { get; set; }
    public required string FileName { get; set; }
    /// <summary>
    /// The folder of a file of a chosen folder download.
    /// </summary>
    public string? Folder { get; set; }
    /// <summary>
    /// What the source finds the file by: its path, or the Dropbox file ID.
    /// </summary>
    public required string Reference { get; set; }
    public required string Error { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset FailedAt { get; set; }
    /// <summary>
    /// The preview in the file storage, null until it has been downloaded.
    /// </summary>
    public string? PreviewFilePath { get; set; }
    public string? PreviewContentType { get; set; }
    /// <summary>
    /// Why the last attempt to download the preview has failed.
    /// </summary>
    public string? PreviewError { get; set; }
    public int PreviewAttempts { get; set; }
    public DateTimeOffset? PreviewAttemptedAt { get; set; }
}
