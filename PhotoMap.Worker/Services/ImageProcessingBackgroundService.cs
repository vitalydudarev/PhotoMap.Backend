using Microsoft.Extensions.Options;
using PhotoMap.Shared.Messages;
using PhotoMap.Shared.Messaging;
using PhotoMap.Shared.Models;
using PhotoMap.Worker.Services.Definitions;

namespace PhotoMap.Worker.Services;

/// <summary>
/// Processes the requested images, several at a time, and sends what it makes of each, a
/// <see cref="PhotoProcessed"/>, to the API. It knows the API only by the messages, so it runs in the API or in a
/// process of its own, PhotoMap.Worker.Host, as many of them as there are partitions of the requests.
/// </summary>
/// <remarks>
/// The requests are taken a batch at a time and acknowledged once the results of the whole batch have been sent:
/// a worker that stops or crashes meanwhile is handed them again, or another worker of the group is (at-least-once).
/// The API skips the photo of a result it has saved already.
/// </remarks>
public class ImageProcessingBackgroundService : BackgroundService
{
    public const string GroupId = "photo-map.image-processors";

    /// <summary>
    /// How long a batch waits for more requests once the first has come.
    /// </summary>
    private static readonly TimeSpan BatchMaxWait = TimeSpan.FromMilliseconds(50);

    private readonly ILogger<ImageProcessingBackgroundService> _logger;
    private readonly IMessageReceiver<ProcessImageRequest> _requestReceiver;
    private readonly IMessageSender<PhotoProcessed> _resultSender;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly int _maxDegreeOfParallelism;
    private readonly TimeSpan _sendRetryDelay;

    public ImageProcessingBackgroundService(
        ILogger<ImageProcessingBackgroundService> logger,
        IMessageReceiver<ProcessImageRequest> requestReceiver,
        IMessageSender<PhotoProcessed> resultSender,
        IImageProcessingService imageProcessingService,
        IOptions<ImageProcessingSettings> settings)
    {
        _logger = logger;
        _requestReceiver = requestReceiver;
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

        // Not cancelled by the service stopping: the images of the batch being processed are finished, and their
        // results sent, rather than abandoned partway and processed again
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };

        try
        {
            while (true)
            {
                // as many as are processed at a time
                var requests = await _requestReceiver.ReceiveBatchAsync(_maxDegreeOfParallelism, BatchMaxWait, stoppingToken);
                if (requests.Count == 0)
                {
                    break;
                }

                var unsentCount = 0;

                await Parallel.ForEachAsync(requests, parallelOptions, async (request, _) =>
                {
                    if (!await ProcessImageAsync(request, stoppingToken))
                    {
                        Interlocked.Increment(ref unsentCount);
                    }
                });

                if (unsentCount > 0 && stoppingToken.IsCancellationRequested)
                {
                    // stopped while results waited to be sent, e.g. with the broker down: the batch is handed again,
                    // to this worker after a restart or to another one of the group, with its downloaded files
                    _logger.LogInformation("Stopping with {UnsentCount} of {RequestCount} results not sent, their " +
                                           "requests are left to be handed again", unsentCount, requests.Count);
                    break;
                }

                // the results of the batch have been sent, it is done with
                _requestReceiver.Acknowledge();
            }
        }
        catch (OperationCanceledException)
        {
            // Prevent throwing if stoppingToken was signaled
        }
    }

    /// <summary>
    /// Processes the image and sends the result, then deletes the downloaded file: deleted before, a request handed
    /// again, its result not having been sent, would find its file gone. Never throws: an exception would stop the
    /// whole loop, while one image failing must only fail that image.
    /// </summary>
    /// <returns>Whether the result has been sent.</returns>
    private async Task<bool> ProcessImageAsync(ProcessImageRequest processImageRequest, CancellationToken stoppingToken)
    {
        PhotoProcessed result;

        try
        {
            // decoding and resizing are CPU-bound, they must not hold up the loop reading the requests
            var processedImage = await Task.Run(() => _imageProcessingService.ProcessImage(processImageRequest));

            result = PhotoProcessed.Success(processImageRequest, processedImage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image {FileName}", processImageRequest.DownloadedFileInfo.ResourceName);

            result = PhotoProcessed.Failure(processImageRequest, "Failed to process the image: " + ex.Message);
        }

        if (!await SendAsync(result, stoppingToken))
        {
            return false;
        }

        DeleteDownloadedFile(processImageRequest.FileName);
        return true;
    }

    /// <summary>
    /// Sends the result until the transport has it, or the service stops: the run that requested the image waits
    /// for it. Meanwhile this image holds one of the images processed at a time, and once all of them wait, the
    /// requests wait to be taken and the runs wait to hand over more: the pipeline slows down to the pace of the
    /// transport (backpressure).
    /// </summary>
    /// <returns>Whether the result has been sent.</returns>
    private async Task<bool> SendAsync(PhotoProcessed result, CancellationToken stoppingToken)
    {
        while (true)
        {
            try
            {
                await _resultSender.SendAsync(result, stoppingToken);
                return true;
            }
            catch (MessagePublishException e)
            {
                _logger.LogWarning(e, "Failed to send the result of {FileName}, trying again in {RetryDelay}",
                    result.FileName, _sendRetryDelay);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to send the result of {FileName}", result.FileName);
                return false;
            }

            try
            {
                await Task.Delay(_sendRetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return false;
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
