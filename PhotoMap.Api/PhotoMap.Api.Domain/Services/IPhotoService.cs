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

        /// <summary>
        /// The photos of the user that are copies of one another, by their groups, the groups of the photos taken
        /// first first, and the photos of a group by their ID. Photos marked as deleted are in no group.
        /// </summary>
        Task<IReadOnlyList<Photo>> GetDuplicatesAsync(long userId);

        /// <summary>
        /// Marks a photo of the user as deleted: it is kept, with its files, but only shows among the deleted photos.
        /// </summary>
        /// <returns>false if the user has no such photo.</returns>
        Task<bool> MarkAsDeletedAsync(long userId, long photoId);

        /// <summary>
        /// Takes a photo of the user back from the deleted photos.
        /// </summary>
        /// <returns>false if the user has no such photo.</returns>
        Task<bool> RestoreAsync(long userId, long photoId);

        /// <returns>The thumbnail files of the deleted photos.</returns>
        Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId);
    }
}
