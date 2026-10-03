using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

/// <summary>
/// Imports the videos of a photo source: a row for each video and its preview image, the video itself stays in the
/// source. Only Yandex.Disk sources have their videos imported.
/// </summary>
public interface IVideoProcessingService
{
    /// <returns>Whether the videos of the photo source can be imported.</returns>
    Task<bool> SupportsVideosAsync(long sourceId);

    /// <returns>false if the command was rejected: Start while the videos of the source are being processed.</returns>
    Task<bool> RunCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command);

    /// <returns>true while the videos of the photo source are being processed for the user.</returns>
    bool IsRunning(long userId, long sourceId);

    /// <summary>
    /// Deletes the videos imported from the photo source, their preview images and the status of their processing.
    /// </summary>
    /// <returns>false, having deleted nothing, while the videos of the source are being processed.</returns>
    Task<bool> DeleteDataAsync(long userId, long sourceId);

    /// <summary>
    /// Deletes the videos of every photo source of every user.
    /// </summary>
    /// <returns>false, having deleted nothing, while the videos of any source are being processed.</returns>
    Task<bool> DeleteAllDataAsync();
}
