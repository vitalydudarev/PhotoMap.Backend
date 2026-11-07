using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Messaging;

namespace PhotoMap.Api.Services
{
    /// <summary>
    /// Checks every so often whether Kafka can be reached, and makes processing available or not accordingly. When
    /// Kafka goes, the runs processing photos and videos are stopped, to be continued once it is back, rather than
    /// failing one request at a time; the frontend is told either way. Runs only with Kafka enabled.
    /// </summary>
    public class ProcessingAvailabilityMonitor : BackgroundService
    {
        public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(3);

        /// <summary>
        /// How many checks in a row must fail before Kafka counts as gone: a single one may be a blip, which would stop
        /// every run for nothing. Coming back takes one check.
        /// </summary>
        public const int FailuresToBeUnavailable = 2;

        private readonly IBrokerHealthCheck _healthCheck;
        private readonly ProcessingAvailability _availability;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IFrontendNotificationService _frontendNotificationService;
        private readonly ILogger<ProcessingAvailabilityMonitor> _logger;
        private int _failuresInARow;

        public ProcessingAvailabilityMonitor(
            IBrokerHealthCheck healthCheck,
            ProcessingAvailability availability,
            IServiceScopeFactory serviceScopeFactory,
            IFrontendNotificationService frontendNotificationService,
            ILogger<ProcessingAvailabilityMonitor> logger)
        {
            _healthCheck = healthCheck;
            _availability = availability;
            _serviceScopeFactory = serviceScopeFactory;
            _frontendNotificationService = frontendNotificationService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (true)
                {
                    try
                    {
                        await CheckAsync();
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        // an exception would stop the application; the next check tries again
                        _logger.LogError(e, "Checking whether Kafka can be reached failed");
                    }

                    await Task.Delay(CheckInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
        }

        /// <summary>
        /// One check, public for the tests to make them one at a time.
        /// </summary>
        public async Task CheckAsync()
        {
            // the check blocks its thread until a broker answers or the timeout
            var reachable = await Task.Run(() => _healthCheck.IsReachable(CheckTimeout));

            if (reachable)
            {
                _failuresInARow = 0;

                if (_availability.Set(true, null))
                {
                    _logger.LogInformation("Kafka can be reached: photos and videos can be processed");
                    await NotifyAsync();
                }

                return;
            }

            _failuresInARow++;

            if (_availability.IsAvailable && _failuresInARow < FailuresToBeUnavailable)
            {
                // maybe a blip: the next check tells
                return;
            }

            var wasAvailable = _availability.IsAvailable;

            if (!_availability.Set(false, ProcessingAvailability.KafkaUnreachableReason))
            {
                // unavailable already, said so before
                return;
            }

            if (wasAvailable)
            {
                var stoppedCount = StopRuns();

                _logger.LogWarning("Kafka cannot be reached: processing photos and videos is unavailable until it is " +
                                   "back, {StoppedCount} runs stopped", stoppedCount);
            }
            else
            {
                // on start: Kafka was not there yet
                _logger.LogWarning("Kafka cannot be reached: processing photos and videos is unavailable until it is");
            }

            await NotifyAsync();
        }

        /// <summary>
        /// The runs record the Stopped status as they end, and can be continued once Kafka is back.
        /// </summary>
        private int StopRuns()
        {
            using var scope = _serviceScopeFactory.CreateScope();

            return scope.ServiceProvider.GetRequiredService<IPhotoSourceProcessingService>().StopAllRuns() +
                   scope.ServiceProvider.GetRequiredService<IVideoProcessingService>().StopAllRuns();
        }

        private async Task NotifyAsync()
        {
            try
            {
                await _frontendNotificationService.SendProcessingAvailabilityAsync(_availability.IsAvailable,
                    _availability.Reason);
            }
            catch (Exception e)
            {
                // the frontend asks for the availability when it loads, it does not depend on the notification
                _logger.LogWarning(e, "Failed to notify the frontend of the availability of processing");
            }
        }
    }
}
