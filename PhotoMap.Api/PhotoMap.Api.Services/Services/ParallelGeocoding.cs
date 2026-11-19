using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Services.Exceptions;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Finds the addresses of GPS locations, of photos or videos, the requests in parallel.
/// </summary>
internal static class ParallelGeocoding
{
    /// <param name="points">What to geocode: the ID of the photo or video, and where it was taken.</param>
    /// <param name="kind">What is geocoded, such as "photo", for the log.</param>
    /// <returns>The responses of the geocoding by ID, how many failed, left for the next round, and the
    /// unavailability of the geocoding, the rest of the points not being sent once it is unavailable.</returns>
    public static async Task<GeocodingResult> RunAsync(IGeocoder geocoder, int maxParallelRequests,
        IReadOnlyList<(long Id, double Latitude, double Longitude)> points, ILogger logger, string kind,
        CancellationToken cancellationToken)
    {
        var geocodings = new ConcurrentDictionary<long, string>();
        var failedCount = 0;
        GeocodingUnavailableException? unavailable = null;

        // the rest of the batch is not sent once the geocoding is unavailable, they would all fail the same way
        using var unavailableCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await Parallel.ForEachAsync(points,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, maxParallelRequests),
                    CancellationToken = unavailableCts.Token
                },
                async (point, token) =>
                {
                    try
                    {
                        geocodings[point.Id] =
                            await geocoder.ReverseGeocodeAsync(point.Latitude, point.Longitude, token);
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
                        logger.LogWarning(e, "Failed to geocode {Kind} {Id}", kind, point.Id);
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // cancelled by the unavailability or the application stopping, which the caller handles once the
            // geocoded ones are saved
        }

        return new GeocodingResult(geocodings.ToDictionary(), failedCount, unavailable);
    }
}

/// <param name="Geocodings">The responses of the geocoding, by the ID of the photo or video.</param>
/// <param name="FailedCount">How many failed to be geocoded, left for the next round.</param>
/// <param name="Unavailable">Why the geocoding became unavailable halfway, null when it did not.</param>
internal record GeocodingResult(
    IReadOnlyDictionary<long, string> Geocodings,
    int FailedCount,
    GeocodingUnavailableException? Unavailable);
