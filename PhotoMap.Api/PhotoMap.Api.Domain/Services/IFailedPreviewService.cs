using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

/// <summary>
/// The previews of the files that failed, of the processing of the photos and of the downloads alike.
/// </summary>
public interface IFailedPreviewService
{
    /// <returns>The failed files of the photo source with their previews, the latest failures first.</returns>
    Task<IReadOnlyList<FailedPreview>> GetAsync(long userId, long photoSourceId);

    /// <returns>The failed file, null when there is no such file.</returns>
    Task<FailedPreview?> GetAsync(FailedPreviewTable table, long id);

    /// <summary>
    /// The failed files whose preview is to be downloaded: not downloaded yet, tried fewer times than given and not
    /// since the time given.
    /// </summary>
    Task<IReadOnlyList<FailedPreview>> GetPendingAsync(int maxAttempts, DateTimeOffset attemptedBefore, int limit);

    /// <returns>false when the failed file is gone, its preview is not wanted any more.</returns>
    Task<bool> SavePreviewAsync(FailedPreviewTable table, long id, string previewFilePath, string contentType);

    /// <summary>
    /// Records why the preview could not be downloaded.
    /// </summary>
    /// <param name="countAttempt">Whether the attempt counts towards the most there are: an account that is not
    /// authorized does not say anything about the file.</param>
    Task PreviewFailedAsync(FailedPreviewTable table, long id, string error, bool countAttempt);

    /// <summary>
    /// Has the previews of the photo source that are not downloaded tried again from the start.
    /// </summary>
    /// <returns>How many previews are tried again.</returns>
    Task<int> ResetAsync(long userId, long photoSourceId);
}
