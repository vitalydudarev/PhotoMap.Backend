using Microsoft.Extensions.Options;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Queues;
using PhotoMap.Worker.Services.Definitions;

namespace PhotoMap.Worker.Services;

public class ImageProcessingBackgroundService : BackgroundService
{
    private readonly ILogger<ImageProcessingBackgroundService> _logger;
    private readonly IMessageQueue<ProcessImageRequest> _requestQueue;
    private readonly IMessageQueue<ProcessedImage> _processedImageQueue;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly int _maxDegreeOfParallelism;

    public ImageProcessingBackgroundService(
        ILogger<ImageProcessingBackgroundService> logger,
        IMessageQueue<ProcessImageRequest> requestQueue,
        IMessageQueue<ProcessedImage> processedImageQueue,
        IImageProcessingService imageProcessingService,
        IOptions<ImageProcessingSettings> settings)
    {
        _logger = logger;
        _requestQueue = requestQueue;
        _processedImageQueue = processedImageQueue;
        _imageProcessingService = imageProcessingService;

        var maxDegreeOfParallelism = settings.Value.MaxDegreeOfParallelism;
        _maxDegreeOfParallelism = maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : Environment.ProcessorCount;
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(ImageProcessingBackgroundService)} is stopping.");

        await base.StopAsync(stoppingToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{ServiceName} is running, processing up to {MaxDegreeOfParallelism} images at a time.",
            nameof(ImageProcessingBackgroundService), _maxDegreeOfParallelism);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = stoppingToken
        };

        try
        {
            await Parallel.ForEachAsync(_requestQueue.ReadAllAsync(stoppingToken), parallelOptions, ProcessImageAsync);
        }
        catch (OperationCanceledException)
        {
            // Prevent throwing if stoppingToken was signaled
        }
    }

    /// <summary>
    /// Never throws: an exception would stop the whole loop, while one image failing must only fail that image.
    /// </summary>
    private async ValueTask ProcessImageAsync(ProcessImageRequest processImageRequest, CancellationToken cancellationToken)
    {
        try
        {
            // decoding and resizing are CPU-bound, they must not hold up the loop reading the queue
            var processedImage = await Task.Run(() => _imageProcessingService.ProcessImage(processImageRequest), cancellationToken);

            await _processedImageQueue.EnqueueAsync(processedImage, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Prevent throwing if stoppingToken was signaled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image {FileName}", processImageRequest.DownloadedFileInfo.ResourceName);

            processImageRequest.Processed?.TrySetResult(false);
        }
        finally
        {
            DeleteDownloadedFile(processImageRequest.FileName);
        }
    }

    /// <summary>
    /// The downloaded file is only needed until it has been read, the photo itself is not stored by the
    /// application. Keeping it would leave the original of every processed photo on the disk.
    /// </summary>
    private void DeleteDownloadedFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to delete the downloaded file {FilePath}", filePath);
        }
    }
}
