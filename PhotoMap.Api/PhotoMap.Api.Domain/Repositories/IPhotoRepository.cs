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

    /// <returns>The thumbnail files of the deleted photos.</returns>
    Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId);
}
