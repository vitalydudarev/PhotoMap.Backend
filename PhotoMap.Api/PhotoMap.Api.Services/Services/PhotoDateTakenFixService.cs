using PhotoMap.Api.Domain.Repositories;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Sets when the photos were taken, for the ones saved as taken at the GPS time some apps write when they have none,
/// a batch at a time; see <see cref="PhotoDateTakenFixer"/>.
/// </summary>
public class PhotoDateTakenFixService
{
    private readonly IPhotoRepository _photoRepository;
    private readonly PhotoYearsCache _yearsCache;

    public PhotoDateTakenFixService(IPhotoRepository photoRepository, PhotoYearsCache yearsCache)
    {
        _photoRepository = photoRepository;
        _yearsCache = yearsCache;
    }

    /// <summary>
    /// Fixes the next photos saved as taken at <see cref="PhotoDateTakenFixer.NoGpsTime"/>. The ones whose EXIF does
    /// not tell when they were taken otherwise are left as they are.
    /// </summary>
    /// <param name="afterId">The ID of the last photo of the batch before, 0 for the first batch.</param>
    /// <returns>The ID of the last photo of the batch, for the next batch to start after, null when none was left,
    /// and how many photos were fixed.</returns>
    public async Task<(long? LastId, int FixedCount)> FixNextAsync(long afterId, int batchSize)
    {
        var photos = await _photoRepository.GetTakenAtAsync(PhotoDateTakenFixer.NoGpsTime, afterId, batchSize);
        if (photos.Count == 0)
        {
            return (null, 0);
        }

        var fixedPhotos = photos
            .Select(a => (Photo: a, DateTaken: PhotoDateTakenFixer.GetDateTaken(a.ExifString)))
            .Where(a => a.DateTaken != null)
            .ToList();

        if (fixedPhotos.Count > 0)
        {
            await _photoRepository.SetDatesTakenAsync(fixedPhotos.ToDictionary(a => a.Photo.Id, a => a.DateTaken!.Value));

            // 1970 may be gone from the years of the user, which only the database can tell
            foreach (var userId in fixedPhotos.Select(a => a.Photo.UserId).Distinct())
            {
                _yearsCache.Invalidate(userId);
            }
        }

        return (photos[^1].Id, fixedPhotos.Count);
    }
}
