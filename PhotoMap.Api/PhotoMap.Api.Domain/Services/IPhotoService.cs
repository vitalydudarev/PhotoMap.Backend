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

        /// <summary>
        /// The photos of the user with a GPS location, but the deleted ones, oldest first, all at once. Only what tells
        /// a photo, where and when it was taken and its name are read.
        /// </summary>
        Task<IReadOnlyList<Photo>> GetGeotaggedAsync(long userId);

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

        /// <returns>The tags of the user that a photo has at least, by name.</returns>
        Task<IReadOnlyList<PhotoTag>> GetTagsAsync(long userId);

        /// <summary>
        /// Replaces the tags of a photo of the user. The names are cleaned, see <see cref="TagNames.Clean"/>, the empty
        /// ones dropped, and a name given twice, whatever its case, taken once. A name the user has a tag of takes
        /// that tag, as it is written.
        /// </summary>
        /// <returns>The names of the tags of the photo, by name, or null if the user has no such photo.</returns>
        Task<IReadOnlyList<string>?> SetTagsAsync(long userId, long photoId, IEnumerable<string> names);

        /// <summary>
        /// Suggests tags for a photo of the user by when and where it was taken, see <c>PhotoTagSuggester</c>.
        /// </summary>
        /// <returns>The tags suggested, the likeliest first, but the ones the photo has; null if the user has no
        /// such photo.</returns>
        Task<IReadOnlyList<TagSuggestion>?> SuggestTagsAsync(long userId, long photoId);

        /// <returns>The thumbnail files of the deleted photos.</returns>
        Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId);
    }
}
