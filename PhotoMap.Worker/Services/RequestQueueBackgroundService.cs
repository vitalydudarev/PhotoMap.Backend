using PhotoMap.Shared.Messaging.MessageSender;
using PhotoMap.Worker.Services.Definitions;

namespace PhotoMap.Worker.Services;

public class RequestQueueBackgroundService : BackgroundService
{
    private readonly ILogger<RequestQueueBackgroundService> _logger;
    private readonly IRequestQueueService _requestQueueService;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IMessagingService _messagingService;

    public RequestQueueBackgroundService(
        ILogger<RequestQueueBackgroundService> logger,
        IRequestQueueService requestQueueService,
        IImageProcessingService imageProcessingService,
        IMessagingService messagingService)
    {
        _logger = logger;
        _requestQueueService = requestQueueService;
        _imageProcessingService = imageProcessingService;
        _messagingService = messagingService;
    }
    
    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(RequestQueueBackgroundService)} is stopping.");

        await base.StopAsync(stoppingToken);
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(RequestQueueBackgroundService)} is running.");

        return ProcessQueueAsync(stoppingToken);
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processImageRequest = await _requestQueueService.DequeueAsync(stoppingToken);

                var processedImage = _imageProcessingService.ProcessImage(processImageRequest.DownloadedFileInfo, processImageRequest.FileName, processImageRequest.Sizes);
                    
                await _messagingService.PublishMessageAsync("pm-ImageProcessed", processedImage);
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
}