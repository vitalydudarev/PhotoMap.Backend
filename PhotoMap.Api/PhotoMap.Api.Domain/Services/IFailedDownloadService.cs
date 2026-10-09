using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

/// <summary>
/// A file of a download, by the folder it is in and its name.
/// </summary>
/// <param name="Folder">The folder of the source the file is in, empty for the folder the photos are processed from.</param>
public sealed record DownloadedFileKey(string Folder, string FileName);

public interface IFailedDownloadService
{
    /// <returns>The files of the downloads of the kind that failed, in the order they first failed.</returns>
    Task<IReadOnlyList<FailedDownload>> GetAsync(long userId, long photoSourceId, SourceDownloadKind kind);

    /// <summary>
    /// Records the outcome of downloading files: the failed ones are added, or their attempts counted when they had
    /// failed before, and the downloaded ones are no longer failed.
    /// </summary>
    /// <param name="failedDownloads">The files that failed, with their error and time of failure.</param>
    /// <param name="downloadedFiles">The files that are downloaded whole.</param>
    Task RecordAsync(long userId, long photoSourceId, SourceDownloadKind kind, IReadOnlyCollection<FailedDownload> failedDownloads,
        IReadOnlyCollection<DownloadedFileKey> downloadedFiles);
}
