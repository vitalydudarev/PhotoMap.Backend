using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Finds the addresses of the GPS locations of the videos, a batch at a time, the requests of a batch in parallel, as
/// <see cref="PhotoGeocodingService"/> does for the photos.
/// </summary>
public class VideoGeocodingService
{
    private readonly IVideoService _videoService;
    private readonly IGeocoder _geocoder;
    private readonly MapboxSettings _settings;
    private readonly ILogger<VideoGeocodingService> _logger;

    public VideoGeocodingService(IVideoService videoService, IGeocoder geocoder, IOptions<MapboxSettings> settings,
        ILogger<VideoGeocodingService> logger)
    {
        _videoService = videoService;
        _geocoder = geocoder;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Geocodes the next videos not geocoded yet, past the given ID, and saves the locations they were taken in along.
    /// The ones that fail are left as they are, for the next round. What was geocoded is saved also when the geocoding
    /// becomes unavailable halfway.
    /// </summary>
    /// <exception cref="GeocodingUnavailableException">No video can be geocoded for now.</exception>
    public async Task<VideoGeocodingBatch> GeocodeNextAsync(long afterId, int batchSize,
        CancellationToken cancellationToken)
    {
        var locations = await _videoService.GetNotGeocodedAsync(afterId, batchSize);
        if (locations.Count == 0)
        {
            return new VideoGeocodingBatch(0, 0, 0, afterId);
        }

        var result = await ParallelGeocoding.RunAsync(_geocoder, _settings.MaxParallelRequests,
            locations.Select(a => (a.VideoId, a.Latitude, a.Longitude)).ToList(), _logger, "video",
            cancellationToken);

        if (result.Geocodings.Count > 0)
        {
            // saved also when stopping, the requests count towards the quota
            await _videoService.SetGeocodingsAsync(result.Geocodings.ToDictionary(a => a.Key,
                a => new PhotoGeocoding(a.Value, GeocodedLocations.Get(a.Value))));
        }

        if (result.Unavailable != null)
        {
            throw result.Unavailable;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new VideoGeocodingBatch(locations.Count, result.Geocodings.Count, result.FailedCount,
            locations[^1].VideoId);
    }
}

/// <param name="VideoCount">How many videos the batch had, 0 when none was left to geocode.</param>
/// <param name="GeocodedCount">How many videos were geocoded.</param>
/// <param name="FailedCount">How many videos failed to be geocoded, left for the next round.</param>
/// <param name="LastVideoId">The ID of the last video of the batch, the next batch starts after.</param>
public record VideoGeocodingBatch(int VideoCount, int GeocodedCount, int FailedCount, long LastVideoId);
