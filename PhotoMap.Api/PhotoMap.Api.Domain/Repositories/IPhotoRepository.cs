using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Repositories;

public interface IPhotoRepository
{
    Task AddRangeAsync(IReadOnlyCollection<Photo> photos, IReadOnlyCollection<OutboxMessage> outboxMessages);
    Task<Photo?> GetAsync(long id);
    Task<IReadOnlySet<string>> GetSavedExternalIdsAsync(long userId, long photoSourceId, IEnumerable<string> externalIds);
    Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, PhotoFilter filter, int top, int skip, PhotoSortOrder sortOrder);
    Task<int> GetTotalCountByUserIdAsync(long userId, PhotoFilter filter);

    /// <returns>The photos of the user with a GPS location, but the deleted ones, oldest first. Only what tells a photo,
    /// where and when it was taken and its name are read.</returns>
    Task<IReadOnlyList<Photo>> GetGeotaggedAsync(long userId);

    /// <returns>The years, in UTC, the photos of the user were taken in.</returns>
    Task<IEnumerable<int>> GetYearsAsync(long userId);

    /// <returns>The photos not yet put in their categories by the given version of the rules, oldest saved first.</returns>
    Task<IReadOnlyList<Photo>> GetNotCategorizedAsync(int version, int top);

    /// <summary>
    /// Replaces the categories of the photos, and records the version of the rules they were put in them by.
    /// </summary>
    Task SetCategoriesAsync(IReadOnlyDictionary<long, IReadOnlyCollection<PhotoCategory>> categoriesByPhotoId, int version);

    /// <returns>The photos taken at exactly the given time, with an ID greater than <paramref name="afterId"/>, by
    /// their ID.</returns>
    Task<IReadOnlyList<Photo>> GetTakenAtAsync(DateTimeOffset dateTimeTaken, long afterId, int top);

    /// <summary>
    /// Sets when the photos were taken. The photos are all updated, or none of them is.
    /// </summary>
    Task SetDatesTakenAsync(IReadOnlyDictionary<long, DateTimeOffset> datesTakenByPhotoId);

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

    /// <returns>The tags of the user that a photo has at least, by name.</returns>
    Task<IReadOnlyList<PhotoTag>> GetTagsAsync(long userId);

    /// <summary>
    /// Replaces the tags of a photo of the user. A name the user has a tag of, whatever its case, takes that tag; the
    /// other names make new tags.
    /// </summary>
    /// <param name="names">The names of the tags, cleaned and of different names; see <see cref="TagNames"/>.</param>
    /// <returns>The names of the tags of the photo, by name, or null if the user has no such photo.</returns>
    Task<IReadOnlyList<string>?> SetTagsAsync(long userId, long photoId, IReadOnlyCollection<string> names);

    /// <summary>
    /// Adds tags to photos of the user, keeping the tags they have. A name the user has a tag of, whatever its case,
    /// takes that tag; the other names make new tags. The photos are all given the tags, or none of them is.
    /// </summary>
    /// <param name="photoIds">The IDs of the photos, each once.</param>
    /// <param name="names">The names of the tags, cleaned and of different names; see <see cref="TagNames"/>.</param>
    Task<AddedTags> AddTagsAsync(long userId, IReadOnlyCollection<long> photoIds, IReadOnlyCollection<string> names);

    /// <returns>The photos of the user with tags, taken within the time or the box of coordinates of the
    /// neighborhood, the photo itself among them if it has tags. At most <paramref name="top"/> of each: of those
    /// taken within the box, the nearest to its middle.</returns>
    Task<IReadOnlyList<TaggedPhoto>> GetTaggedPhotosAsync(long userId, PhotoNeighborhood neighborhood, int top);

    /// <returns>The thumbnail files of the deleted photos.</returns>
    Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId);

    /// <summary>
    /// Deletes only the camera uploads of the photo source, the photos of the folder it is processed from, or only
    /// its other uploads, the photos of the other uploads.
    /// </summary>
    /// <returns>The thumbnail files of the deleted photos.</returns>
    Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId, bool fromOtherUploads);
}
