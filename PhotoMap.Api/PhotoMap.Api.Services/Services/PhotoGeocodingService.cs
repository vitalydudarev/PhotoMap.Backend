using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Services.Exceptions;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Finds the addresses of the GPS locations of the photos, a batch at a time, the requests of a batch in parallel.
/// </summary>
public class PhotoGeocodingService
{
    private readonly IPhotoRepository _photoRepository;
    private readonly IGeocoder _geocoder;
    private readonly MapboxSettings _settings;
    private readonly ILogger<PhotoGeocodingService> _logger;

    public PhotoGeocodingService(IPhotoRepository photoRepository, IGeocoder geocoder,
        IOptions<MapboxSettings> settings, ILogger<PhotoGeocodingService> logger)
    {
        _photoRepository = photoRepository;
        _geocoder = geocoder;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Geocodes the next photos not geocoded yet, past the given ID, and saves the locations they were taken in along.
    /// The ones that fail are left as they are, for the next round. What was geocoded is saved also when the geocoding
    /// becomes unavailable halfway.
    /// </summary>
    /// <exception cref="GeocodingUnavailableException">No photo can be geocoded for now.</exception>
    public async Task<PhotoGeocodingBatch> GeocodeNextAsync(long afterId, int batchSize,
        CancellationToken cancellationToken)
    {
        var locations = await _photoRepository.GetNotGeocodedAsync(afterId, batchSize);
        if (locations.Count == 0)
        {
            return new PhotoGeocodingBatch(0, 0, 0, afterId);
        }

        var result = await ParallelGeocoding.RunAsync(_geocoder, _settings.MaxParallelRequests,
            locations.Select(a => (a.PhotoId, a.Latitude, a.Longitude)).ToList(), _logger, "photo",
            cancellationToken);

        if (result.Geocodings.Count > 0)
        {
            // saved also when stopping, the requests count towards the quota
            await _photoRepository.SetGeocodingsAsync(result.Geocodings.ToDictionary(a => a.Key,
                a => new PhotoGeocoding(a.Value, GeocodedLocations.Get(a.Value))));
        }

        if (result.Unavailable != null)
        {
            throw result.Unavailable;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new PhotoGeocodingBatch(locations.Count, result.Geocodings.Count, result.FailedCount,
            locations[^1].PhotoId);
    }
}
/// <param name="PhotoCount">How many photos the batch had, 0 when none was left to geocode.</param>
/// <param name="GeocodedCount">How many photos were geocoded.</param>
/// <param name="FailedCount">How many photos failed to be geocoded, left for the next round.</param>
/// <param name="LastPhotoId">The ID of the last photo of the batch, the next batch starts after.</param>
public record PhotoGeocodingBatch(int PhotoCount, int GeocodedCount, int FailedCount, long LastPhotoId);
