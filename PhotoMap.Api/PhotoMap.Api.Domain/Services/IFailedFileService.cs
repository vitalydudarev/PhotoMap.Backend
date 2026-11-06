using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IFailedFileService
{
    /// <returns>The camera uploads of the photo source that failed, in the order they first failed.</returns>
    Task<IReadOnlyCollection<FailedFile>> GetAsync(long userId, long photoSourceId);

    /// <returns>The other uploads of the photo source that failed, in the order they first failed.</returns>
    Task<IReadOnlyCollection<FailedFile>> GetOtherUploadsAsync(long userId, long photoSourceId);

    /// <summary>
    /// How many camera uploads, or other uploads, of the photo source have failed and are not saved yet. The failed
    /// count of their processing is this count, each file counted once however many times it has failed.
    /// </summary>
    Task<int> CountAsync(long userId, long photoSourceId, bool fromOtherUploads);

    /// <summary>
    /// Records the outcome of a page of files: the failed ones are added, or their attempts counted when they had
    /// failed before, and the saved ones are no longer failed.
    /// </summary>
    /// <param name="failedFiles">The files that failed, with their stage, error and time of failure, and whether they
    /// are other uploads.</param>
    /// <param name="savedExternalIds">The files that were saved.</param>
    Task RecordAsync(long userId, long photoSourceId, IReadOnlyCollection<FailedFile> failedFiles,
        IReadOnlyCollection<string> savedExternalIds);

    /// <summary>
    /// Deletes the failed files of the photo source, the camera uploads and the other uploads alike.
    /// </summary>
    Task DeleteByPhotoSourceAsync(long userId, long photoSourceId);

    /// <summary>
    /// Deletes only the failed camera uploads of the photo source, or only its failed other uploads.
    /// </summary>
    Task DeleteByPhotoSourceAsync(long userId, long photoSourceId, bool fromOtherUploads);
}
