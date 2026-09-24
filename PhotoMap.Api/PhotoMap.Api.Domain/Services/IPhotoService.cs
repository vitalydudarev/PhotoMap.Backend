using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services
{
    public interface IPhotoService
    {
        Task AddAsync(Photo photo);

        /// <summary>
        /// Saves the photos in one go: they are all saved, or none of them is.
        /// </summary>
        Task AddRangeAsync(IReadOnlyCollection<Photo> photos);

        Task<Photo?> GetAsync(long id);

        Task<Photo?> GetByFileNameAsync(string fileName);

        Task<bool> ExistsAsync(long userId, long photoSourceId, string externalId);

        Task<IReadOnlySet<string>> GetSavedExternalIdsAsync(long userId, long photoSourceId, IEnumerable<string> externalIds);

        Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, int top, int skip, PhotoSortOrder sortOrder);
        
        Task<int> GetTotalCountByUserIdAsync(long userId);

        Task DeleteByUserId(long userId);

        /// <returns>The thumbnail files of the deleted photos.</returns>
        Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId);

        Task DeleteAllAsync();
    }
}
