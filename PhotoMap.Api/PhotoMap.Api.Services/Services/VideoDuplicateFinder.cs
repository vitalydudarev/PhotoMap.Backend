using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Tells the videos of a user that are copies of one another: of the same size, to the byte, first, which sets most
/// of them apart, and then of the same file name, whatever its case. A video copied to another folder, or uploaded
/// twice, is one; a video edited, or exported again, is not.
/// </summary>
public static class VideoDuplicateFinder
{
    /// <returns>The group of duplicates of each video, the ID of the first video of the group; null for a video
    /// without duplicates.</returns>
    public static IReadOnlyDictionary<long, long?> FindDuplicateGroups(IEnumerable<VideoDuplicateKey> videos)
    {
        var duplicateGroupIds = new Dictionary<long, long?>();

        foreach (var sameSize in videos.GroupBy(a => (a.UserId, a.Size)))
        {
            foreach (var sameName in sameSize.GroupBy(a => a.FileName, StringComparer.OrdinalIgnoreCase))
            {
                var videoIds = sameName.Select(a => a.Id).ToList();
                long? duplicateGroupId = videoIds.Count > 1 ? videoIds.Min() : null;

                foreach (var videoId in videoIds)
                {
                    duplicateGroupIds[videoId] = duplicateGroupId;
                }
            }
        }

        return duplicateGroupIds;
    }
}
