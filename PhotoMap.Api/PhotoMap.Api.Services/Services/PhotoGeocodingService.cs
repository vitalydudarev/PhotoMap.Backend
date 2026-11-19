using System.Collections.Concurrent;
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

        var geocodings = new ConcurrentDictionary<long, string>();
        var failedCount = 0;
        GeocodingUnavailableException? unavailable = null;

        // the rest of the batch is not sent once the geocoding is unavailable, they would all fail the same way
        using var unavailableCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await Parallel.ForEachAsync(locations,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, _settings.MaxParallelRequests),
                    CancellationToken = unavailableCts.Token
                },
                async (location, token) =>
                {
                    try
                    {
                        geocodings[location.PhotoId] =
                            await _geocoder.ReverseGeocodeAsync(location.Latitude, location.Longitude, token);
                    }
                    catch (GeocodingUnavailableException e)
                    {
                        Interlocked.CompareExchange(ref unavailable, e, null);
                        await unavailableCts.CancelAsync();
                    }
                    catch (Exception e) when (e is HttpRequestException ||
                                              (e is TaskCanceledException && !token.IsCancellationRequested))
                    {
                        // an error of the request, or its timeout
                        Interlocked.Increment(ref failedCount);
                        _logger.LogWarning(e, "Failed to geocode photo {PhotoId}", location.PhotoId);
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // cancelled by the unavailability or the application stopping, thrown below once the geocoded ones are
            // saved
        }

        if (!geocodings.IsEmpty)
        {
            // saved also when stopping, the requests count towards the quota
            await _photoRepository.SetGeocodingsAsync(geocodings.ToDictionary(a => a.Key,
                a => new PhotoGeocoding(a.Value, GeocodedLocations.Get(a.Value))));
        }

        if (unavailable != null)
        {
            throw unavailable;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new PhotoGeocodingBatch(locations.Count, geocodings.Count, failedCount, locations[^1].PhotoId);
    }
}

/// <param name="PhotoCount">How many photos the batch had, 0 when none was left to geocode.</param>
/// <param name="GeocodedCount">How many photos were geocoded.</param>
/// <param name="FailedCount">How many photos failed to be geocoded, left for the next round.</param>
/// <param name="LastPhotoId">The ID of the last photo of the batch, the next batch starts after.</param>
public record PhotoGeocodingBatch(int PhotoCount, int GeocodedCount, int FailedCount, long LastPhotoId);
