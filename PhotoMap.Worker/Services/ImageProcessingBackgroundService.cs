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

    public ImageProcessingBackgroundService(
        ILogger<ImageProcessingBackgroundService> logger,
        IMessageQueue<ProcessImageRequest> requestQueue,
        IMessageQueue<ProcessedImage> processedImageQueue,
        IImageProcessingService imageProcessingService)
    {
        _logger = logger;
        _requestQueue = requestQueue;
        _processedImageQueue = processedImageQueue;
        _imageProcessingService = imageProcessingService;
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(ImageProcessingBackgroundService)} is stopping.");

        await base.StopAsync(stoppingToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(ImageProcessingBackgroundService)} is running.");

        try
        {
            await foreach (var processImageRequest in _requestQueue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    var processedImage = _imageProcessingService.ProcessImage(processImageRequest);

                    await _processedImageQueue.EnqueueAsync(processedImage, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Prevent throwing if stoppingToken was signaled
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Prevent throwing if stoppingToken was signaled
        }
    }
}
