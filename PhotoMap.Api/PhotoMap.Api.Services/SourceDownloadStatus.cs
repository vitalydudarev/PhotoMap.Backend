using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services;

/// <summary>
/// Where the last download of a photo source has got to.
/// </summary>
/// <param name="Folder">The folder a chosen folder download takes its files from, null for the folder the photos are
/// processed from and for a retry of the failed files.</param>
/// <param name="RetryingFailed">The download takes only the files that failed before, rather than the folder.</param>
/// <param name="TotalCount">The files of the folder, or the failed files, to download.</param>
/// <param name="DownloadedCount">The files downloaded whole, the ones found downloaded already among them.</param>
/// <param name="AlreadyDownloadedCount">The files found downloaded whole when the download started.</param>
/// <param name="FailedCount">The files that failed to download this time.</param>
public sealed record SourceDownloadStatus(
    PhotoSourceStatus Status,
    SourceDownloadContent Content,
    string? Folder,
    bool RetryingFailed,
    int TotalCount,
    int DownloadedCount,
    int AlreadyDownloadedCount,
    int FailedCount,
    DateTimeOffset LastUpdatedAt);
