using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Queues;
using PhotoMap.Worker;
using PhotoMap.Worker.Services;
using PhotoMap.Worker.Services.Definitions;

namespace PhotoMap.Api.Services.Tests;

public class ImageProcessingBackgroundServiceTests : IDisposable
{
    private const int MaxDegreeOfParallelism = 4;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string _directory = Directory.CreateTempSubdirectory(nameof(ImageProcessingBackgroundServiceTests)).FullName;

    [Fact]
    public async Task ExecuteAsync_ShouldProcessImagesInParallel()
    {
        // Arrange: every image blocks until all of them are being processed, which only happens in parallel
        using var imagesInProcessing = new CountdownEvent(MaxDegreeOfParallelism);
        var imageProcessingService = new BlockingImageProcessingService(imagesInProcessing, Timeout);

        var requestQueue = new ChannelMessageQueue<ProcessImageRequest>();
        var processedImageQueue = new ChannelMessageQueue<ProcessedImage>();
        var service = CreateService(requestQueue, processedImageQueue, imageProcessingService);

        var requests = Enumerable.Range(0, MaxDegreeOfParallelism).Select(a => CreateRequest(a.ToString())).ToList();

        // Act
        await service.StartAsync(CancellationToken.None);

        foreach (var request in requests)
        {
            await requestQueue.EnqueueAsync(request);
        }

        var processedImages = await ReadAsync(processedImageQueue, requests.Count);

        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(imagesInProcessing.IsSet, $"{MaxDegreeOfParallelism} images were expected to be processed at a time");
        Assert.Equal(requests.Count, processedImages.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDeleteDownloadedFile()
    {
        // Arrange
        var requestQueue = new ChannelMessageQueue<ProcessImageRequest>();
        var processedImageQueue = new ChannelMessageQueue<ProcessedImage>();
        var service = CreateService(requestQueue, processedImageQueue, new ImageProcessingServiceStub());

        var processedRequest = CreateRequest("processed");
        var failedRequest = CreateRequest(ImageProcessingServiceStub.FailingFileName);

        // Act
        await service.StartAsync(CancellationToken.None);

        await requestQueue.EnqueueAsync(processedRequest);
        await requestQueue.EnqueueAsync(failedRequest);

        await ReadAsync(processedImageQueue, 1);
        Assert.False((await failedRequest.Processed!.Task.WaitAsync(Timeout)).Succeeded);

        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.False(File.Exists(processedRequest.FileName));
        Assert.False(File.Exists(failedRequest.FileName));
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private static ImageProcessingBackgroundService CreateService(
        IMessageQueue<ProcessImageRequest> requestQueue,
        IMessageQueue<ProcessedImage> processedImageQueue,
        IImageProcessingService imageProcessingService)
    {
        var settings = Options.Create(new ImageProcessingSettings { MaxDegreeOfParallelism = MaxDegreeOfParallelism });

        return new ImageProcessingBackgroundService(NullLogger<ImageProcessingBackgroundService>.Instance, requestQueue,
            processedImageQueue, imageProcessingService, settings);
    }

    private ProcessImageRequest CreateRequest(string fileName)
    {
        var filePath = Path.Combine(_directory, fileName);
        File.WriteAllBytes(filePath, [1, 2, 3]);

        return new ProcessImageRequest
        {
            DownloadedFileInfo = new DownloadedFileInfo(fileName, "/" + fileName, DateTime.UtcNow, "id:" + fileName),
            FileName = filePath,
            Sizes = [256],
            UserId = 1,
            PhotoSourceId = 1,
            PhotoSourceName = "Dropbox",
            Processed = new TaskCompletionSource<ProcessingResult>(TaskCreationOptions.RunContinuationsAsynchronously)
        };
    }

    private static async Task<List<ProcessedImage>> ReadAsync(IMessageQueue<ProcessedImage> queue, int count)
    {
        using var cancellationTokenSource = new CancellationTokenSource(Timeout);

        var processedImages = new List<ProcessedImage>();

        await foreach (var processedImage in queue.ReadAllAsync(cancellationTokenSource.Token))
        {
            processedImages.Add(processedImage);

            if (processedImages.Count == count)
            {
                break;
            }
        }

        return processedImages;
    }

    private class ImageProcessingServiceStub : IImageProcessingService
    {
        public const string FailingFileName = "failing";

        public virtual ProcessedImage ProcessImage(ProcessImageRequest request)
        {
            var fileName = request.DownloadedFileInfo.ResourceName;
            if (fileName == FailingFileName)
            {
                throw new Exception("Unsupported image");
            }

            return new ProcessedImage
            {
                FileName = fileName,
                UserId = request.UserId,
                PhotoSourceId = request.PhotoSourceId,
                PhotoSourceName = request.PhotoSourceName,
                ContentHash = "hash",
                Thumbs = new Dictionary<int, byte[]>(),
                Processed = request.Processed
            };
        }
    }

    private class BlockingImageProcessingService : ImageProcessingServiceStub
    {
        private readonly CountdownEvent _imagesInProcessing;
        private readonly TimeSpan _timeout;

        public BlockingImageProcessingService(CountdownEvent imagesInProcessing, TimeSpan timeout)
        {
            _imagesInProcessing = imagesInProcessing;
            _timeout = timeout;
        }

        public override ProcessedImage ProcessImage(ProcessImageRequest request)
        {
            _imagesInProcessing.Signal();
            _imagesInProcessing.Wait(_timeout);

            return base.ProcessImage(request);
        }
    }
}
