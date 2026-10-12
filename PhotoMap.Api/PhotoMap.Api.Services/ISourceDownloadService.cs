using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services;

/// <summary>
/// Downloads the original photos and videos of a photo source into the file storage, as they are: from the folder its
/// photos are processed from, or from a folder the user chooses. The downloads of each kind run, and keep their
/// failed files, apart from one another and from the processing of the source.
/// </summary>
public interface ISourceDownloadService
{
    /// <returns>Whether the files of the photo source can be downloaded by a download of the kind.</returns>
    Task<bool> SupportsDownloadsAsync(long sourceId, SourceDownloadKind kind);

    /// <summary>
    /// Starts downloading the files of the folder the photos of the source are processed from. The files of the
    /// folder are told apart into the ones downloaded whole already, which are skipped, and the missing ones, which
    /// are downloaded, those that failed before among them. A download stopped carries on where it left off.
    /// </summary>
    /// <returns>false if the download of the kind is already running.</returns>
    Task<bool> StartAsync(long userId, long sourceId, SourceDownloadContent content);

    /// <summary>
    /// Starts downloading the files of a folder of the photo source the user has chosen, and of the folders within
    /// it at any depth, as <see cref="StartAsync"/> does those of the folder the photos are processed from. The files
    /// keep the folders they are in on the source.
    /// </summary>
    /// <param name="folder">The folder, as <see cref="Services.YandexDiskPaths.NormalizeFolder"/> takes it.</param>
    /// <returns>false if the download of the kind is already running.</returns>
    /// <exception cref="ArgumentException">The folder cannot be downloaded from.</exception>
    Task<bool> StartFolderAsync(long userId, long sourceId, string folder, SourceDownloadContent content);

    /// <summary>
    /// Starts downloading again only the files of the downloads of the kind that failed, without listing a folder.
    /// </summary>
    /// <returns>false if the download of the kind is already running.</returns>
    Task<bool> RetryFailedAsync(long userId, long sourceId, SourceDownloadKind kind);

    /// <returns>The files of the downloads of the kind that failed and are not downloaded yet.</returns>
    Task<IReadOnlyList<FailedDownload>> GetFailedDownloadsAsync(long userId, long sourceId, SourceDownloadKind kind);

    /// <summary>
    /// Stops the download of the kind, which records the Stopped status when it has finished.
    /// </summary>
    void Stop(long userId, long sourceId, SourceDownloadKind kind);

    /// <returns>true while the download of the kind is running for the user.</returns>
    bool IsRunning(long userId, long sourceId, SourceDownloadKind kind);

    /// <returns>The status of the last download of the kind, null when there has been none since the application
    /// started.</returns>
    SourceDownloadStatus? GetStatus(long userId, long sourceId, SourceDownloadKind kind);

    /// <returns>The folder the downloads of the kind save their files to. A chosen folder is saved in the folders of
    /// its path within it.</returns>
    Task<string> GetTargetFolderAsync(long userId, long sourceId, SourceDownloadKind kind);
}
