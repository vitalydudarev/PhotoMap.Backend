using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Saves the locations of the photos geocoded before the locations were saved along with the geocoding, a batch at a
/// time; see <see cref="GeocodedLocations"/>.
/// </summary>
public class PhotoLocationsBackfillService
{
    private readonly IPhotoRepository _photoRepository;

    public PhotoLocationsBackfillService(IPhotoRepository photoRepository)
    {
        _photoRepository = photoRepository;
    }

    /// <summary>
    /// Replaces the locations of the next geocoded photos with the ones their geocoding tells.
    /// </summary>
    /// <param name="afterId">The ID of the last photo of the batch before, 0 for the first batch.</param>
    /// <returns>The ID of the last photo of the batch, for the next batch to start after, null when none was left,
    /// and how many photos were taken in a location at least.</returns>
    public async Task<(long? LastId, int LocatedCount)> BackfillNextAsync(long afterId, int batchSize)
    {
        var photos = await _photoRepository.GetGeocodedAsync(afterId, batchSize);
        if (photos.Count == 0)
        {
            return (null, 0);
        }

        var locations = photos.ToDictionary(a => a.PhotoId,
            a => (IReadOnlyCollection<GeocodedLocation>)GeocodedLocations.Get(a.Geocoding));

        await _photoRepository.SetLocationsAsync(locations);

        return (photos[^1].PhotoId, locations.Values.Count(a => a.Count > 0));
    }
}
