using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IFrontendNotificationService
{
    Task SendErrorAsync(long userId, long sourceId, string errorText);
    Task SendProgressAsync(UserPhotoSourceStatus status);

    /// <summary>
    /// The error the processing of the videos of the photo source has failed with.
    /// </summary>
    Task SendVideoErrorAsync(long userId, long sourceId, string errorText);

    /// <summary>
    /// The status and counters of the processing of the videos of the photo source.
    /// </summary>
    Task SendVideoProgressAsync(UserPhotoSourceStatus status);
}
