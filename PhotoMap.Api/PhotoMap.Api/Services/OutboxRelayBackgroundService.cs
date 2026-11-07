using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services
{
    /// <summary>
    /// Runs the <see cref="OutboxRelay"/>: on start, for the messages left from before; whenever the signal tells
    /// that messages have been added; and every so often in case a signal has been missed. Deletes the messages
    /// published a while ago.
    /// </summary>
    public class OutboxRelayBackgroundService : BackgroundService
    {
        private const int BatchSize = 200;

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How long the relay waits after a failure, e.g. the broker being down, before trying again.
        /// </summary>
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

        /// <summary>
        /// How long the published messages are kept, to look into what has been published.
        /// </summary>
        private static readonly TimeSpan Retention = TimeSpan.FromDays(1);

        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

        private readonly OutboxSignal _signal;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<OutboxRelayBackgroundService> _logger;
        private DateTimeOffset _lastCleanup = DateTimeOffset.MinValue;

        public OutboxRelayBackgroundService(
            OutboxSignal signal,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<OutboxRelayBackgroundService> logger)
        {
            _signal = signal;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{ServiceName} is running.", nameof(OutboxRelayBackgroundService));

            try
            {
                while (true)
                {
                    OutboxRelayResult result;

                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var relay = scope.ServiceProvider.GetRequiredService<OutboxRelay>();

                        result = await relay.PublishPendingAsync(BatchSize, stoppingToken);

                        await DeletePublishedIfDueAsync(scope.ServiceProvider.GetRequiredService<IOutboxService>());
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        // an exception would stop the application; the messages wait in the database meanwhile
                        _logger.LogError(e, "The outbox relay failed, trying again in {RetryDelay}", RetryDelay);

                        await Task.Delay(RetryDelay, stoppingToken);
                        continue;
                    }

                    if (result.Failed > 0)
                    {
                        await Task.Delay(RetryDelay, stoppingToken);
                    }
                    else if (result.Read < BatchSize)
                    {
                        // all the messages have been published, else the next batch is taken right away
                        await WaitForMessagesAsync(stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
        }

        private async Task DeletePublishedIfDueAsync(IOutboxService outboxService)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastCleanup < CleanupInterval)
            {
                return;
            }

            var deletedCount = await outboxService.DeletePublishedAsync(now - Retention);
            _lastCleanup = now;

            if (deletedCount > 0)
            {
                _logger.LogInformation("Deleted {DeletedCount} outbox messages published over {Retention} ago",
                    deletedCount, Retention);
            }
        }

        /// <summary>
        /// Waits for messages to be added, or for the poll interval to go by.
        /// </summary>
        private async Task WaitForMessagesAsync(CancellationToken stoppingToken)
        {
            using var intervalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            intervalCancellationTokenSource.CancelAfter(PollInterval);

            try
            {
                await _signal.WaitAsync(intervalCancellationTokenSource.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // the interval has gone by
            }
        }
    }
}
