using PhotoMap.Api.Domain.Repositories;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Puts the photos in their groups of duplicates, as <see cref="PhotoDuplicateFinder"/> tells them.
/// </summary>
public class PhotoDuplicatesService : IDuplicateGroupsService
{
    private readonly IPhotoRepository _photoRepository;

    public PhotoDuplicatesService(IPhotoRepository photoRepository)
    {
        _photoRepository = photoRepository;
    }

    public async Task<int> UpdateDuplicateGroupsAsync()
    {
        var photos = await _photoRepository.GetDuplicateKeysAsync();
        var duplicateGroupIds = PhotoDuplicateFinder.FindDuplicateGroups(photos);

        var changed = photos
            .Where(a => a.DuplicateGroupId != duplicateGroupIds[a.Id])
            .ToDictionary(a => a.Id, a => duplicateGroupIds[a.Id]);

        if (changed.Count > 0)
        {
            await _photoRepository.SetDuplicateGroupsAsync(changed);
        }

        return changed.Count;
    }
}
