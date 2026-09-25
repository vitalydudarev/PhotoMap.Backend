using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services
{
    public interface IPhotoService
    {
        /// <summary>
        /// Saves the photos in one go: they are all saved, or none of them is.
        /// </summary>
        Task AddRangeAsync(IReadOnlyCollection<Photo> photos);

        Task<Photo?> GetAsync(long id);

        Task<IReadOnlySet<string>> GetSavedExternalIdsAsync(long userId, long photoSourceId, IEnumerable<string> externalIds);

        Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, PhotoFilter filter, int top, int skip, PhotoSortOrder sortOrder);

        Task<int> GetTotalCountByUserIdAsync(long userId, PhotoFilter filter);

        /// <returns>The years, in UTC, the photos of the user were taken in, oldest first.</returns>
        Task<IReadOnlyList<int>> GetYearsAsync(long userId);

        /// <returns>The thumbnail files of the deleted photos.</returns>
        Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId);
    }
}
