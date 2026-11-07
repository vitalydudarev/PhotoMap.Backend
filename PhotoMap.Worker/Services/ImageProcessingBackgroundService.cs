using Microsoft.Extensions.Options;
using PhotoMap.Shared.Messages;
using PhotoMap.Shared.Messaging;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Queues;
using PhotoMap.Worker.Services.Definitions;

namespace PhotoMap.Worker.Services;

/// <summary>
/// Processes the requested images, several at a time, and sends what it makes of each, a
/// <see cref="PhotoProcessed"/>, to the API. It knows the API only by the messages: nothing of the API goes with a
/// request, so the worker can run in a process of its own.
/// </summary>
public class ImageProcessingBackgroundService : BackgroundService
{
    private readonly ILogger<ImageProcessingBackgroundService> _logger;
    private readonly IMessageQueue<ProcessImageRequest> _requestQueue;
    private readonly IMessageSender<PhotoProcessed> _resultSender;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly int _maxDegreeOfParallelism;
    private readonly TimeSpan _sendRetryDelay;

    public ImageProcessingBackgroundService(
        ILogger<ImageProcessingBackgroundService> logger,
        IMessageQueue<ProcessImageRequest> requestQueue,
        IMessageSender<PhotoProcessed> resultSender,
        IImageProcessingService imageProcessingService,
        IOptions<ImageProcessingSettings> settings)
    {
        _logger = logger;
        _requestQueue = requestQueue;
        _resultSender = resultSender;
        _imageProcessingService = imageProcessingService;

        var maxDegreeOfParallelism = settings.Value.MaxDegreeOfParallelism;
        _maxDegreeOfParallelism = maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : Environment.ProcessorCount;
        _sendRetryDelay = settings.Value.SendRetryDelay;
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
        PhotoProcessed result;

        try
        {
            // decoding and resizing are CPU-bound, they must not hold up the loop reading the queue
            var processedImage = await Task.Run(() => _imageProcessingService.ProcessImage(processImageRequest), cancellationToken);

            result = PhotoProcessed.Success(processImageRequest, processedImage);
        }
        catch (OperationCanceledException)
        {
            // Prevent throwing if stoppingToken was signaled
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image {FileName}", processImageRequest.DownloadedFileInfo.ResourceName);

            result = PhotoProcessed.Failure(processImageRequest, "Failed to process the image: " + ex.Message);
        }
        finally
        {
            DeleteDownloadedFile(processImageRequest.FileName);
        }

        await SendAsync(result, cancellationToken);
    }

    /// <summary>
    /// Sends the result until the transport has it: the run that requested the image waits for it. Meanwhile this
    /// image holds one of the images processed at a time, and once all of them wait, the requests wait in the queue
    /// and the runs wait to hand over more: the pipeline slows down to the pace of the transport (backpressure).
    /// </summary>
    private async Task SendAsync(PhotoProcessed result, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await _resultSender.SendAsync(result, cancellationToken);
                return;
            }
            catch (MessagePublishException e)
            {
                _logger.LogWarning(e, "Failed to send the result of {FileName}, trying again in {RetryDelay}",
                    result.FileName, _sendRetryDelay);
            }
            catch (OperationCanceledException)
            {
                // stopping: the run is stopped as well, it does not wait for the result any longer
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to send the result of {FileName}", result.FileName);
                return;
            }

            try
            {
                await Task.Delay(_sendRetryDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
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
