using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services
{
    /// <summary>
    /// Goes through the photos geocoded so far and saves the locations they were taken in, which the gallery filters
    /// them by. Runs on start, once: completed, it is recorded in the database and not run again. The photos geocoded
    /// since have their locations saved along with the geocoding.
    /// </summary>
    public class PhotoLocationsBackfillBackgroundService : BackgroundService
    {
        public const string TaskName = "save-photo-locations-from-geocoding";

        private const int BatchSize = 500;

        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<PhotoLocationsBackfillBackgroundService> _logger;

        public PhotoLocationsBackfillBackgroundService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<PhotoLocationsBackfillBackgroundService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(PhotoLocationsBackfillBackgroundService)} is running.");

            try
            {
                while (true)
                {
                    try
                    {
                        await BackfillAllAsync(stoppingToken);
                        return;
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        // an exception would stop the application; the photos are tried again later instead
                        _logger.LogError(e, "Failed to save the locations of the photos, trying again in {RetryDelay}",
                            RetryDelay);

                        await Task.Delay(RetryDelay, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
        }

        private async Task BackfillAllAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var maintenanceTasks = scope.ServiceProvider.GetRequiredService<IMaintenanceTaskRepository>();
                if (await maintenanceTasks.IsCompletedAsync(TaskName))
                {
                    return;
                }
            }

            var total = 0;
            long afterId = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var backfillService = scope.ServiceProvider.GetRequiredService<PhotoLocationsBackfillService>();

                var (lastId, locatedCount) = await backfillService.BackfillNextAsync(afterId, BatchSize);
                if (lastId == null)
                {
                    break;
                }

                afterId = lastId.Value;
                total += locatedCount;
            }

            // stopped half-way, the task runs again on the next start; the photos done are just done again
            stoppingToken.ThrowIfCancellationRequested();

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var maintenanceTasks = scope.ServiceProvider.GetRequiredService<IMaintenanceTaskRepository>();
                await maintenanceTasks.MarkCompletedAsync(TaskName);
            }

            _logger.LogInformation("Saved the locations of {PhotoCount} photos, the backfill will not run again",
                total);
        }
    }
}
