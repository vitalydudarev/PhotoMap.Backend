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
    /// <param name="years">The years, in UTC, the videos were taken in, all of them when empty.</param>
    /// <param name="locations">The locations the videos were taken in, see <see cref="LocationFilter"/>.</param>
    Task<IReadOnlyList<Video>> GetByUserIdAsync(long userId, IReadOnlyCollection<string> folderPaths,
        IReadOnlyCollection<int> years, LocationFilter locations, int top, int skip, PhotoSortOrder sortOrder);

    /// <param name="folderPaths">The folders to count the videos of, all of them when empty.</param>
    /// <param name="years">The years, in UTC, to count the videos taken in, all of them when empty.</param>
    /// <param name="locations">The locations to count the videos taken in, see <see cref="LocationFilter"/>.</param>
    /// <summary>
    /// What the videos the filter takes can still be narrowed down to, see <see cref="VideoFacets"/>. The filter is
    /// that of <see cref="GetByUserIdAsync"/>.
    /// </summary>
    Task<VideoFacets> GetFacetsAsync(long userId, IReadOnlyCollection<string> folderPaths,
        IReadOnlyCollection<int> years, LocationFilter locations);

    Task<int> GetTotalCountByUserIdAsync(long userId, IReadOnlyCollection<string> folderPaths,
        IReadOnlyCollection<int> years, LocationFilter locations);

    /// <summary>
    /// The locations the videos of the user were taken in, by level, from the countries down, and by name, with how
    /// many videos were taken in each. A video is in a location once it is geocoded.
    /// </summary>
    Task<IReadOnlyList<Location>> GetLocationsAsync(long userId);

    /// <summary>
    /// The next videos of every user with a GPS location and not geocoded yet, past the ID given, by their IDs.
    /// </summary>
    Task<IReadOnlyList<VideoGpsLocation>> GetNotGeocodedAsync(long afterId, int top);

    /// <summary>
    /// Saves the geocoding of the videos, and the locations it tells they were taken in, by their IDs. The locations
    /// are saved once, under the level and the key of <see cref="GeocodedLocation"/>.
    /// </summary>
    Task SetGeocodingsAsync(IReadOnlyDictionary<long, PhotoGeocoding> geocodingsByVideoId);

    /// <summary>
    /// The years, in UTC, the videos of the user were taken in, oldest first.
    /// </summary>
    Task<IReadOnlyList<int>> GetYearsAsync(long userId);

    /// <summary>
    /// The folders the videos of the user are in, by name.
    /// </summary>
    Task<IReadOnlyList<string>> GetFolderPathsAsync(long userId);

    /// <summary>
    /// The videos of the user that have duplicates, by their groups, the groups of the largest videos first, and
    /// the videos of a group by their ID.
    /// </summary>
    Task<IReadOnlyList<Video>> GetDuplicatesAsync(long userId);

    /// <summary>
    /// What tells the duplicates of every video of every user.
    /// </summary>
    Task<IReadOnlyList<VideoDuplicateKey>> GetDuplicateKeysAsync();

    /// <summary>
    /// Puts the videos in the groups of duplicates given, null to take one out of its group.
    /// </summary>
    Task SetDuplicateGroupsAsync(IReadOnlyDictionary<long, long?> duplicateGroupIdsByVideoId);

    /// <summary>
    /// The IDs, of the ones given, of the videos of the photo source saved already.
    /// </summary>
    Task<IReadOnlySet<string>> GetSavedExternalIdsAsync(long userId, long photoSourceId, IEnumerable<string> externalIds);

    /// <summary>
    /// The IDs of the videos of the photo source saved without the metadata of their files, by the IDs the photo
    /// source assigned.
    /// </summary>
    Task<IReadOnlyDictionary<string, long>> GetIdsWithoutExifAsync(long userId, long photoSourceId);

    /// <summary>
    /// The next videos of every user saved without the metadata of their files, past the ID given, by their IDs.
    /// </summary>
    Task<IReadOnlyList<Video>> GetWithoutExifAsync(long afterId, int count);

    /// <summary>
    /// Saves the metadata read from the files of the videos, by their IDs, see <see cref="VideoExif.ApplyTo"/>. A
    /// video whose GPS location changes is geocoded again.
    /// </summary>
    Task UpdateExifAsync(IReadOnlyDictionary<long, VideoExif> exifByVideoId);

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
