using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IUserPhotoSourceService
{
    Task<IEnumerable<UserPhotoSource>> GetUserPhotoSourcesAsync(long userId);

    /// <summary>
    /// The photo sources of every user, by the IDs of the user and of the source.
    /// </summary>
    Task<IReadOnlyCollection<(long UserId, long PhotoSourceId)>> GetAllUserPhotoSourceIdsAsync();

    Task<UserAuthResult?> GetAuthResultAsync(long userId, long photoSourceId);
    Task<UserPhotoSourceStatus?> GetUserPhotoStatusAsync(long userId, long photoSourceId);
    Task<UserPhotoSourceState?> GetUserPhotoStateAsync(long userId, long photoSourceId);
    Task UpdateAuthResultAsync(long userId, long photoSourceId, UserAuthResult userAuthResult);
    Task UpdateUserPhotoStateAsync(long userId, long photoSourceId, string? state);
    Task DeleteUserPhotoStatusAsync(long userId, long photoSourceId);
    Task UpdateUserPhotoStatusAsync(UserPhotoSourceStatus status);

    /// <summary>
    /// The status of the last processing of the photos of the photo source outside the folder it is processed from,
    /// null when they have never been processed.
    /// </summary>
    Task<UserPhotoSourceStatus?> GetOtherFoldersStatusAsync(long userId, long photoSourceId);

    Task UpdateOtherFoldersStatusAsync(UserPhotoSourceStatus status);
    Task DeleteOtherFoldersStatusAsync(long userId, long photoSourceId);

    /// <summary>
    /// Marks the runs recorded as in progress as paused, of the folder the sources are processed from and of the
    /// other folders, returns how many there were.
    /// </summary>
    Task<int> PauseInProgressAsync();
}