using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IFrontendNotificationService
{
    Task SendErrorAsync(long userId, long sourceId, string errorText);
    Task SendProgressAsync(UserPhotoSourceStatus status);

    /// <summary>
    /// The status and counters of the photo source while a run retries its failed files, with how far the retry has got.
    /// </summary>
    Task SendProgressAsync(UserPhotoSourceStatus status, RetryProgress retry);

    /// <summary>
    /// The error the processing of the videos of the photo source has failed with.
    /// </summary>
    Task SendVideoErrorAsync(long userId, long sourceId, string errorText);

    /// <summary>
    /// The status and counters of the processing of the videos of the photo source.
    /// </summary>
    Task SendVideoProgressAsync(UserPhotoSourceStatus status);

    /// <summary>
    /// The error the processing of the photos of the photo source outside the folder it is processed from has
    /// failed with.
    /// </summary>
    Task SendOtherUploadsErrorAsync(long userId, long sourceId, string errorText);

    /// <summary>
    /// The status and counters of the processing of the photos of the photo source outside the folder it is
    /// processed from.
    /// </summary>
    Task SendOtherUploadsProgressAsync(UserPhotoSourceStatus status);

    /// <summary>
    /// The status and counters of the other uploads of the photo source while a run retries the ones that failed,
    /// with how far the retry has got.
    /// </summary>
    Task SendOtherUploadsProgressAsync(UserPhotoSourceStatus status, RetryProgress retry);

    /// <summary>
    /// The error the download of the files of the photo source of the kind has failed with.
    /// </summary>
    Task SendDownloadErrorAsync(long userId, long sourceId, SourceDownloadKind kind, string errorText);

    /// <summary>
    /// The status and counters of the download of the files of the photo source of the kind.
    /// </summary>
    Task SendDownloadProgressAsync(UserPhotoSourceStatus status, SourceDownloadKind kind);
}
