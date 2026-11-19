using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services
{
    /// <summary>
    /// Finds the addresses of the GPS locations of the photos, and then of the videos: on start, the ones saved before
    /// and not geocoded yet, and then the ones saved since, whenever photos or videos are saved. Those that fail to be
    /// geocoded are tried again on the next round.
    /// </summary>
    public class GeocodingBackgroundService : BackgroundService
    {
        private const int BatchSize = 100;

        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

        private readonly GeocodingSignal _signal;
        private readonly IGeocoder _geocoder;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<GeocodingBackgroundService> _logger;

        public GeocodingBackgroundService(
            GeocodingSignal signal,
            IGeocoder geocoder,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<GeocodingBackgroundService> logger)
        {
            _signal = signal;
            _geocoder = geocoder;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_geocoder.IsConfigured)
            {
                _logger.LogWarning("The Mapbox access token is not configured ({Section}:AccessToken), the photos and " +
                                   "videos are not geocoded", MapboxSettings.SectionName);
                return;
            }

            _logger.LogInformation($"{nameof(GeocodingBackgroundService)} is running.");

            try
            {
                while (true)
                {
                    try
                    {
                        await GeocodePhotosAsync(stoppingToken);
                        await GeocodeVideosAsync(stoppingToken);
                    }
                    catch (GeocodingUnavailableException e)
                    {
                        // the round starts over then, the photos and videos geocoded so far are not looked at again
                        _logger.LogWarning("{Reason}, geocoding again in {RetryAfter}", e.Message, e.RetryAfter);

                        await Task.Delay(e.RetryAfter, stoppingToken);
                        continue;
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        // an exception would stop the application; the photos left are tried again later instead
                        _logger.LogError(e, "Failed to geocode the photos and videos, trying again in {RetryDelay}",
                            RetryDelay);

                        await Task.Delay(RetryDelay, stoppingToken);
                        continue;
                    }

                    await _signal.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
        }

        /// <summary>
        /// Goes through the photos not geocoded yet once, by their ID, so that the ones failing are not tried over and
        /// over within a round.
        /// </summary>
        private async Task GeocodePhotosAsync(CancellationToken stoppingToken)
        {
            var geocoded = 0;
            var failed = 0;
            var afterId = 0L;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var geocodingService = scope.ServiceProvider.GetRequiredService<PhotoGeocodingService>();

                    var batch = await geocodingService.GeocodeNextAsync(afterId, BatchSize, stoppingToken);
                    if (batch.PhotoCount == 0)
                    {
                        break;
                    }

                    geocoded += batch.GeocodedCount;
                    failed += batch.FailedCount;
                    afterId = batch.LastPhotoId;
                }
            }
            finally
            {
                if (geocoded > 0 || failed > 0)
                {
                    _logger.LogInformation("Geocoded {PhotoCount} photos, {FailedCount} failed", geocoded, failed);
                }
            }
        }

        /// <summary>
        /// Goes through the videos not geocoded yet once, by their ID, as <see cref="GeocodePhotosAsync"/> does.
        /// </summary>
        private async Task GeocodeVideosAsync(CancellationToken stoppingToken)
        {
            var geocoded = 0;
            var failed = 0;
            var afterId = 0L;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var geocodingService = scope.ServiceProvider.GetRequiredService<VideoGeocodingService>();

                    var batch = await geocodingService.GeocodeNextAsync(afterId, BatchSize, stoppingToken);
                    if (batch.VideoCount == 0)
                    {
                        break;
                    }

                    geocoded += batch.GeocodedCount;
                    failed += batch.FailedCount;
                    afterId = batch.LastVideoId;
                }
            }
            finally
            {
                if (geocoded > 0 || failed > 0)
                {
                    _logger.LogInformation("Geocoded {VideoCount} videos, {FailedCount} failed", geocoded, failed);
                }
            }
        }
    }
}
