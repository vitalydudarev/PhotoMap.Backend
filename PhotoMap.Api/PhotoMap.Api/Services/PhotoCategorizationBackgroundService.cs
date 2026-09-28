using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services
{
    /// <summary>
    /// Puts the photos in their categories: on start, the ones saved before, or categorized by older rules, and then
    /// the ones saved since, whenever photos are saved.
    /// </summary>
    public class PhotoCategorizationBackgroundService : BackgroundService
    {
        private const int BatchSize = 500;

        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

        private readonly PhotoCategorizationSignal _signal;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<PhotoCategorizationBackgroundService> _logger;

        public PhotoCategorizationBackgroundService(
            PhotoCategorizationSignal signal,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<PhotoCategorizationBackgroundService> logger)
        {
            _signal = signal;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(PhotoCategorizationBackgroundService)} is running.");

            try
            {
                while (true)
                {
                    try
                    {
                        await CategorizeAllAsync(stoppingToken);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        // an exception would stop the application; the photos left are tried again later instead
                        _logger.LogError(e, "Failed to categorize the photos, trying again in {RetryDelay}", RetryDelay);

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

        private async Task CategorizeAllAsync(CancellationToken stoppingToken)
        {
            var total = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var categorizationService = scope.ServiceProvider.GetRequiredService<PhotoCategorizationService>();

                var count = await categorizationService.CategorizeNextAsync(BatchSize);
                if (count == 0)
                {
                    break;
                }

                total += count;
            }

            if (total > 0)
            {
                _logger.LogInformation("Categorized {PhotoCount} photos", total);
            }
        }
    }
}
