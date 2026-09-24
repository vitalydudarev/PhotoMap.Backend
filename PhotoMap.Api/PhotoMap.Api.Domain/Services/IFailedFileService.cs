using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IFailedFileService
{
    Task<IReadOnlyCollection<FailedFile>> GetAsync(long userId, long photoSourceId);

    /// <summary>
    /// Records the outcome of a page of files: the failed ones are added, or their attempts counted when they had
    /// failed before, and the saved ones are no longer failed.
    /// </summary>
    /// <param name="failedFiles">The files that failed, with their stage, error and time of failure.</param>
    /// <param name="savedExternalIds">The files that were saved.</param>
    Task RecordAsync(long userId, long photoSourceId, IReadOnlyCollection<FailedFile> failedFiles,
        IReadOnlyCollection<string> savedExternalIds);

    Task DeleteByPhotoSourceAsync(long userId, long photoSourceId);
}
