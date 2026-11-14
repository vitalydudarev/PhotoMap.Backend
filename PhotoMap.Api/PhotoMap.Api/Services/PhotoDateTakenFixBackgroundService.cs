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
    /// Goes through the photos saved as taken at 1970-01-01 00:00:00, the GPS time some apps, Instagram among them,
    /// write when they have none, and sets when they were taken from the DateTimeOriginal of their EXIF. Runs on start,
    /// once: completed, it is recorded in the database and not run again. The worker no longer takes that GPS time, so
    /// the photos saved since need no fixing.
    /// </summary>
    public class PhotoDateTakenFixBackgroundService : BackgroundService
    {
        public const string TaskName = "fix-photo-date-taken-from-no-gps-time";

        private const int BatchSize = 500;

        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<PhotoDateTakenFixBackgroundService> _logger;

        public PhotoDateTakenFixBackgroundService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<PhotoDateTakenFixBackgroundService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(PhotoDateTakenFixBackgroundService)} is running.");

            try
            {
                while (true)
                {
                    try
                    {
                        await FixAllAsync(stoppingToken);
                        return;
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        // an exception would stop the application; the photos are tried again later instead
                        _logger.LogError(e, "Failed to fix when the photos were taken, trying again in {RetryDelay}",
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

        private async Task FixAllAsync(CancellationToken stoppingToken)
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
                var fixService = scope.ServiceProvider.GetRequiredService<PhotoDateTakenFixService>();

                var (lastId, fixedCount) = await fixService.FixNextAsync(afterId, BatchSize);
                if (lastId == null)
                {
                    break;
                }

                afterId = lastId.Value;
                total += fixedCount;
            }

            // stopped half-way, the task runs again on the next start
            stoppingToken.ThrowIfCancellationRequested();

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var maintenanceTasks = scope.ServiceProvider.GetRequiredService<IMaintenanceTaskRepository>();
                await maintenanceTasks.MarkCompletedAsync(TaskName);
            }

            _logger.LogInformation("Fixed when {PhotoCount} photos were taken, the fix will not run again", total);
        }
    }
}
