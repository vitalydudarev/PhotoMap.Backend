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
    /// Downloads the previews of the files that failed: on start, whenever the signal tells that files have failed,
    /// which a processing or a download run does as it records them, and every so often for the previews to try
    /// again.
    /// </summary>
    public class FailedPreviewsBackgroundService : BackgroundService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

        /// <summary>
        /// A run records its failures as it goes; waiting a little after the first lets the next ones come down to
        /// the same round.
        /// </summary>
        private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(5);

        private readonly FailedPreviewsSignal _signal;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<FailedPreviewsBackgroundService> _logger;

        public FailedPreviewsBackgroundService(
            FailedPreviewsSignal signal,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<FailedPreviewsBackgroundService> logger)
        {
            _signal = signal;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("The previews of the failed files are downloaded.");

            try
            {
                while (true)
                {
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var downloader = scope.ServiceProvider.GetRequiredService<FailedPreviewsDownloader>();

                        await downloader.DownloadPendingAsync(stoppingToken);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        // an exception would stop the application; the previews are tried again later instead
                        _logger.LogError(e, "Downloading the previews of the failed files failed, trying again in {RetryDelay}",
                            RetryDelay);

                        await Task.Delay(RetryDelay, stoppingToken);
                        continue;
                    }

                    await WaitForFailuresAsync(stoppingToken);
                    await Task.Delay(SettleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
        }

        /// <summary>
        /// Waits for files to fail, or for the interval to go by, for the previews to try again.
        /// </summary>
        private async Task WaitForFailuresAsync(CancellationToken stoppingToken)
        {
            using var intervalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            intervalCancellationTokenSource.CancelAfter(CheckInterval);

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
