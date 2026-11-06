using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

/// <summary>
/// Processes the photos of a photo source: those of the folder it is processed from, and, by a run of its own, those
/// outside it. Only Yandex.Disk sources have their other folders processed.
/// </summary>
public interface IPhotoSourceProcessingService
{
    /// <returns>false if the command was rejected: Start while the processing of the source is running.</returns>
    Task<bool> RunCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command);

    /// <returns>true while the photo source is being processed for the user.</returns>
    bool IsRunning(long userId, long sourceId);

    /// <returns>Whether the photos of the other folders of the photo source can be processed.</returns>
    Task<bool> SupportsOtherFoldersAsync(long sourceId);

    /// <returns>false if the command was rejected: Start while the other folders of the source are being processed.</returns>
    Task<bool> RunOtherFoldersCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command);

    /// <returns>true while the other folders of the photo source are being processed for the user.</returns>
    bool IsOtherFoldersRunning(long userId, long sourceId);
}
