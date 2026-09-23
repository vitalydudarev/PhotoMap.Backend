using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

public interface IPhotoSourceProcessingService
{
    /// <returns>false if the command was rejected: Start while the processing of the source is running.</returns>
    Task<bool> RunCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command);

    /// <returns>true while the photo source is being processed for the user.</returns>
    bool IsRunning(long userId, long sourceId);
}
