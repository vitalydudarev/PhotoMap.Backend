using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services;

/// <summary>
/// What has become of a file the last download has found missing from the file storage.
/// </summary>
public enum MissingDownloadStatus
{
    /// <summary>
    /// Not downloaded yet: the download is still to get to it, or has stopped before it did.
    /// </summary>
    Pending,
    Downloaded,
    Failed
}

/// <summary>
/// A file of the source the last download has found missing from the file storage, or there only in part, and has
/// tried to download.
/// </summary>
/// <param name="Folder">The folder of the source the file is in, empty for the folder the photos are processed from.</param>
/// <param name="Error">What the file has failed with, null unless it has failed.</param>
public sealed record MissingDownload(
    string Folder,
    string FileName,
    long Size,
    SourceDownloadContent Content,
    MissingDownloadStatus Status,
    string? Error);
