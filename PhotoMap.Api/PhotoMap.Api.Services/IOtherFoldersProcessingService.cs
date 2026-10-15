using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

/// <summary>
/// Imports the photos of a photo source outside the folder it is processed from, by a run of its own. Only Yandex.Disk
/// sources have their other folders imported.
/// </summary>
public interface IOtherFoldersProcessingService
{
    /// <returns>Whether the photos of the other folders of the photo source can be imported.</returns>
    Task<bool> SupportsOtherFoldersAsync(long sourceId);

    /// <returns>false if the command was rejected: Start while the other folders of the source are being processed.</returns>
    Task<bool> RunCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command);

    /// <returns>true while the other folders of the photo source are being processed for the user.</returns>
    bool IsRunning(long userId, long sourceId);
}
