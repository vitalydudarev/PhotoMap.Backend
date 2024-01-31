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
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(RequestQueueBackgroundService)} is running.");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processImageRequest = _requestQueueService.Dequeue();
                if (processImageRequest != null)
                {
                    var processedImage = _imageProcessingService.ProcessImage(processImageRequest.DownloadedFileInfo, processImageRequest.Sizes);
                    
                    await _messagingService.PublishMessageAsync("pm-ProcessedImage", processedImage);
                    // process it
                }
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

        await Task.CompletedTask;
    }
}