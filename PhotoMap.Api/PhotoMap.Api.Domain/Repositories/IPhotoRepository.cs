using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Repositories;

public interface IPhotoRepository
{
    Task AddRangeAsync(IReadOnlyCollection<Photo> photos);
    Task<Photo?> GetAsync(long id);
    Task<IReadOnlySet<string>> GetSavedExternalIdsAsync(long userId, long photoSourceId, IEnumerable<string> externalIds);
    Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, PhotoFilter filter, int top, int skip, PhotoSortOrder sortOrder);
    Task<int> GetTotalCountByUserIdAsync(long userId, PhotoFilter filter);

    /// <returns>The years, in UTC, the photos of the user were taken in.</returns>
    Task<IEnumerable<int>> GetYearsAsync(long userId);

    /// <returns>The photos not yet put in their categories by the given version of the rules, oldest saved first.</returns>
    Task<IReadOnlyList<Photo>> GetNotCategorizedAsync(int version, int top);

    /// <summary>
    /// Replaces the categories of the photos, and records the version of the rules they were put in them by.
    /// </summary>
    Task SetCategoriesAsync(IReadOnlyDictionary<long, IReadOnlyCollection<PhotoCategory>> categoriesByPhotoId, int version);

    /// <returns>The photos of the user that have copies, by their groups, the groups of the photos taken first first, and
    /// the photos of a group by their ID.</returns>
    Task<IReadOnlyList<Photo>> GetDuplicatesAsync(long userId);

    /// <returns>What tells the copies of every photo of every user.</returns>
    Task<IReadOnlyList<PhotoDuplicateKey>> GetDuplicateKeysAsync();

    /// <summary>
    /// Puts the photos in the groups of duplicates given, null to take one out of its group.
    /// </summary>
    Task SetDuplicateGroupsAsync(IReadOnlyDictionary<long, long?> duplicateGroupIdsByPhotoId);

    /// <summary>
    /// Marks a photo of the user as deleted, or no longer deleted when <paramref name="deletedOn"/> is null.
    /// </summary>
    /// <returns>false if the user has no such photo.</returns>
    Task<bool> SetDeletedOnAsync(long userId, long photoId, DateTimeOffset? deletedOn);

    /// <returns>The thumbnail files of the deleted photos.</returns>
    Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId);
}
