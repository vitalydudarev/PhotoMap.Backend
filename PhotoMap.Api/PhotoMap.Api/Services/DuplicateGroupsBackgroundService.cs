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
    /// Keeps the groups of duplicates of one kind of file up to date: on start, and then whenever the signal tells
    /// that files were saved or deleted.
    /// </summary>
    /// <typeparam name="TSignal">Tells that files of the kind were saved or deleted.</typeparam>
    /// <typeparam name="TService">Puts the files of the kind in their groups.</typeparam>
    public class DuplicateGroupsBackgroundService<TSignal, TService> : BackgroundService
        where TSignal : DuplicatesSignal
        where TService : IDuplicateGroupsService
    {
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

        /// <summary>
        /// A run saves its files a hundred at a time; waiting a little after the first save lets the next ones
        /// come down to the same round, rather than a round for each.
        /// </summary>
        private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(10);

        private readonly TSignal _signal;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<DuplicateGroupsBackgroundService<TSignal, TService>> _logger;

        public DuplicateGroupsBackgroundService(
            TSignal signal,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<DuplicateGroupsBackgroundService<TSignal, TService>> logger)
        {
            _signal = signal;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Duplicate groups of {Service} are kept up to date.", typeof(TService).Name);

            try
            {
                while (true)
                {
                    try
                    {
                        await UpdateDuplicateGroupsAsync();
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        // an exception would stop the application; the duplicates are looked for again later instead
                        _logger.LogError(e, "{Service} failed to look for duplicates, trying again in {RetryDelay}",
                            typeof(TService).Name, RetryDelay);

                        await Task.Delay(RetryDelay, stoppingToken);
                        continue;
                    }

                    await _signal.WaitAsync(stoppingToken);
                    await Task.Delay(SettleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
        }

        private async Task UpdateDuplicateGroupsAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var duplicatesService = scope.ServiceProvider.GetRequiredService<TService>();

            var count = await duplicatesService.UpdateDuplicateGroupsAsync();
            if (count > 0)
            {
                _logger.LogInformation("{Service} updated the duplicate groups of {FileCount} files",
                    typeof(TService).Name, count);
            }
        }
    }
}
