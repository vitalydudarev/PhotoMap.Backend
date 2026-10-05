using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Tells the photos of a user that are copies of one another: of the same contents, to the byte, by the hash taken
/// of them, whatever their names, folders or photo sources. A photo marked as deleted is a copy of none, so deleting
/// a copy leaves the others alone in their group, and out of it once only one is left.
/// </summary>
public static class PhotoDuplicateFinder
{
    /// <returns>The group of duplicates of each photo, the ID of the first photo of the group; null for a photo
    /// without copies.</returns>
    public static IReadOnlyDictionary<long, long?> FindDuplicateGroups(IReadOnlyCollection<PhotoDuplicateKey> photos)
    {
        var duplicateGroupIds = photos.ToDictionary(a => a.Id, _ => (long?)null);

        var copies = photos
            .Where(a => !a.IsDeleted && !string.IsNullOrEmpty(a.ContentHash))
            .GroupBy(a => (a.UserId, a.ContentHash))
            .Where(a => a.Count() > 1);

        foreach (var sameContents in copies)
        {
            var duplicateGroupId = sameContents.Min(a => a.Id);

            foreach (var photo in sameContents)
            {
                duplicateGroupIds[photo.Id] = duplicateGroupId;
            }
        }

        return duplicateGroupIds;
    }
}
