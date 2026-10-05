using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Puts the videos in their groups of duplicates, as <see cref="VideoDuplicateFinder"/> tells them.
/// </summary>
public class VideoDuplicatesService : IDuplicateGroupsService
{
    private readonly IVideoService _videoService;

    public VideoDuplicatesService(IVideoService videoService)
    {
        _videoService = videoService;
    }

    public async Task<int> UpdateDuplicateGroupsAsync()
    {
        var videos = await _videoService.GetDuplicateKeysAsync();
        var duplicateGroupIds = VideoDuplicateFinder.FindDuplicateGroups(videos);

        var changed = videos
            .Where(a => a.DuplicateGroupId != duplicateGroupIds[a.Id])
            .ToDictionary(a => a.Id, a => duplicateGroupIds[a.Id]);

        if (changed.Count > 0)
        {
            await _videoService.SetDuplicateGroupsAsync(changed);
        }

        return changed.Count;
    }
}
