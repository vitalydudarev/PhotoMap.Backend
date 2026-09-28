using PhotoMap.Api.Domain.Repositories;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Puts the photos in their categories by the current <see cref="PhotoCategorizer"/> rules, a batch at a time.
/// </summary>
public class PhotoCategorizationService
{
    private readonly IPhotoRepository _photoRepository;

    public PhotoCategorizationService(IPhotoRepository photoRepository)
    {
        _photoRepository = photoRepository;
    }

    /// <summary>
    /// Categorizes the next photos not categorized by the current rules yet.
    /// </summary>
    /// <returns>How many photos were categorized, 0 when none was left.</returns>
    public async Task<int> CategorizeNextAsync(int batchSize)
    {
        var photos = await _photoRepository.GetNotCategorizedAsync(PhotoCategorizer.Version, batchSize);
        if (photos.Count == 0)
        {
            return 0;
        }

        var categoriesByPhotoId = photos.ToDictionary(a => a.Id,
            a => PhotoCategorizer.Categorize(a.FileName, a.ExifString));

        await _photoRepository.SetCategoriesAsync(categoriesByPhotoId, PhotoCategorizer.Version);

        return photos.Count;
    }
}
