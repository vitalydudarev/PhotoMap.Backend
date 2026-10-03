using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IVideoService
{
    Task AddRangeAsync(IReadOnlyCollection<Video> videos);
    Task<Video?> GetAsync(long id);

    /// <summary>
    /// The videos of the user, by the date they were taken, with the ID breaking ties.
    /// </summary>
    /// <param name="folderPaths">The folders to take the videos from, all of them when empty.</param>
    Task<IReadOnlyList<Video>> GetByUserIdAsync(long userId, IReadOnlyCollection<string> folderPaths, int top, int skip,
        PhotoSortOrder sortOrder);

    /// <param name="folderPaths">The folders to count the videos of, all of them when empty.</param>
    Task<int> GetTotalCountByUserIdAsync(long userId, IReadOnlyCollection<string> folderPaths);

    /// <summary>
    /// The folders the videos of the user are in, by name.
    /// </summary>
    Task<IReadOnlyList<string>> GetFolderPathsAsync(long userId);

    /// <summary>
    /// The IDs, of the ones given, of the videos of the photo source saved already.
    /// </summary>
    Task<IReadOnlySet<string>> GetSavedExternalIdsAsync(long userId, long photoSourceId, IEnumerable<string> externalIds);

    /// <summary>
    /// Deletes the videos of the photo source.
    /// </summary>
    /// <returns>The paths of their preview images, which are left for the caller to delete.</returns>
    Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId);

    /// <summary>
    /// The status of the last processing of the videos of the photo source, null when they have never been processed.
    /// </summary>
    Task<UserPhotoSourceStatus?> GetStatusAsync(long userId, long photoSourceId);

    Task UpdateStatusAsync(UserPhotoSourceStatus status);
    Task DeleteStatusAsync(long userId, long photoSourceId);

    /// <summary>
    /// Marks the runs recorded as in progress as paused, returns how many there were.
    /// </summary>
    Task<int> PauseInProgressAsync();
}
