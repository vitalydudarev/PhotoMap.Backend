using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PhotoMap.Api.Services;

/// <summary>
/// Reads the metadata of the files of the videos saved without it, on start, see <see cref="VideoExifBackfiller"/>.
/// Once all of them have it, a start finds none and is done at once; the videos that failed are tried again on the
/// next start, and by the next run of the videos of their source.
/// </summary>
public class VideoExifBackgroundService : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<VideoExifBackgroundService> _logger;

    public VideoExifBackgroundService(IServiceScopeFactory serviceScopeFactory, ILogger<VideoExifBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // the start of the application is not held up by the first batch
        await Task.Yield();

        try
        {
            while (true)
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var backfiller = scope.ServiceProvider.GetRequiredService<VideoExifBackfiller>();

                    var result = await backfiller.ReadAllAsync(stoppingToken);

                    _logger.LogInformation(
                        "Read the metadata of {ReadCount} videos saved without it, {FailedCount} failed and {SkippedCount} were skipped",
                        result.ReadCount, result.FailedCount, result.SkippedCount);

                    return;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // an exception would stop the application; the videos are tried again later instead
                    _logger.LogError(e, "Failed to read the metadata of the videos, trying again in {RetryDelay}", RetryDelay);

                    await Task.Delay(RetryDelay, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // the application is stopping, what has been read is saved a batch at a time
        }
    }
}
