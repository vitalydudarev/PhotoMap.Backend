namespace PhotoMap.Api.DTOs;

/// <summary>
/// A file of a photo source that failed, with its preview.
/// </summary>
public class FailedPreviewDto
{
    /// <summary>
    /// <c>files</c> for a file of the processing of the photos, <c>downloads</c> for one of a download.
    /// </summary>
    public string Table { get; set; } = null!;
    public long Id { get; set; }
    /// <summary>
    /// What has failed: <c>Processing</c>, <c>Download</c> or <c>FolderDownload</c>.
    /// </summary>
    public string Origin { get; set; } = null!;
    public string FileName { get; set; } = null!;
    /// <summary>
    /// The folder of a file of a chosen folder download.
    /// </summary>
    public string? Folder { get; set; }
    public string Error { get; set; } = null!;
    public int Attempts { get; set; }
    public string FailedAt { get; set; } = null!;
    /// <summary>
    /// The URL of the preview, null until it has been downloaded.
    /// </summary>
    public string? PreviewUrl { get; set; }
    /// <summary>
    /// <c>Ready</c>, <c>Pending</c> while it is still tried, or <c>Unavailable</c> once it has been given up on.
    /// </summary>
    public string PreviewStatus { get; set; } = null!;
    /// <summary>
    /// Why the last attempt to download the preview has failed.
    /// </summary>
    public string? PreviewError { get; set; }
}
