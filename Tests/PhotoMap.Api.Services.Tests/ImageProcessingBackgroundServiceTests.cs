using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PhotoMap.Shared.Messages;
using PhotoMap.Shared.Messaging;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Queues;
using PhotoMap.Worker;
using PhotoMap.Worker.Services;
using PhotoMap.Worker.Services.Definitions;
using PhotoMap.Worker.Services.Implementations;

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
        var results = new ChannelMessageTransport<PhotoProcessed>();
        var service = CreateService(requestQueue, results, imageProcessingService);

        var requests = Enumerable.Range(0, MaxDegreeOfParallelism).Select(a => CreateRequest(a.ToString())).ToList();

        // Act
        await service.StartAsync(CancellationToken.None);

        foreach (var request in requests)
        {
            await requestQueue.EnqueueAsync(request);
        }

        var received = await ReadAsync(results, requests.Count);

        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(imagesInProcessing.IsSet, $"{MaxDegreeOfParallelism} images were expected to be processed at a time");
        Assert.Equal(requests.Select(a => a.RequestId).Order(), received.Select(a => a.RequestId).Order());
        Assert.All(received, a => Assert.True(a.Succeeded));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDeleteDownloadedFile()
    {
        // Arrange
        var requestQueue = new ChannelMessageQueue<ProcessImageRequest>();
        var results = new ChannelMessageTransport<PhotoProcessed>();
        var service = CreateService(requestQueue, results, new ImageProcessingServiceStub());

        var processedRequest = CreateRequest("processed");
        var failedRequest = CreateRequest(ImageProcessingServiceStub.FailingFileName);

        // Act
        await service.StartAsync(CancellationToken.None);

        await requestQueue.EnqueueAsync(processedRequest);
        await requestQueue.EnqueueAsync(failedRequest);

        var received = await ReadAsync(results, 2);

        await service.StopAsync(CancellationToken.None);

        // Assert: the failure is sent as a result as well, with what went wrong
        var failed = Assert.Single(received, a => a.RequestId == failedRequest.RequestId);
        Assert.False(failed.Succeeded);
        Assert.Contains("Unsupported image", failed.Error);
        Assert.True(Assert.Single(received, a => a.RequestId == processedRequest.RequestId).Succeeded);
        Assert.False(File.Exists(processedRequest.FileName));
        Assert.False(File.Exists(failedRequest.FileName));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFailTheImage_WhenTheDownloadedFileIsEmpty()
    {
        // Arrange
        var requestQueue = new ChannelMessageQueue<ProcessImageRequest>();
        var results = new ChannelMessageTransport<PhotoProcessed>();
        var imageProcessingService = new ImageProcessingService(NullLogger<ImageProcessingService>.Instance,
            new Mock<IExifExtractor>().Object);
        var service = CreateService(requestQueue, results, imageProcessingService);

        var request = CreateRequest("empty", []);

        // Act
        await service.StartAsync(CancellationToken.None);

        await requestQueue.EnqueueAsync(request);

        var result = Assert.Single(await ReadAsync(results, 1));

        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(request.RequestId, result.RequestId);
        Assert.False(result.Succeeded);
        Assert.Contains("empty", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSendTheResultAgain_WhenTheTransportFailsIt()
    {
        // Arrange: the broker is down for the first attempt
        var requestQueue = new ChannelMessageQueue<ProcessImageRequest>();
        var results = new ChannelMessageTransport<PhotoProcessed>();
        var sender = new FlakySender(results, failures: 1);

        var service = CreateService(requestQueue, sender, new ImageProcessingServiceStub());
        var request = CreateRequest("retried");

        // Act
        await service.StartAsync(CancellationToken.None);

        await requestQueue.EnqueueAsync(request);

        var result = Assert.Single(await ReadAsync(results, 1));

        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(request.RequestId, result.RequestId);
        Assert.True(result.Succeeded);
        Assert.Equal(2, sender.Attempts);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private static ImageProcessingBackgroundService CreateService(
        IMessageQueue<ProcessImageRequest> requestQueue,
        IMessageSender<PhotoProcessed> resultSender,
        IImageProcessingService imageProcessingService)
    {
        var settings = Options.Create(new ImageProcessingSettings
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            SendRetryDelay = TimeSpan.FromMilliseconds(10)
        });

        return new ImageProcessingBackgroundService(NullLogger<ImageProcessingBackgroundService>.Instance, requestQueue,
            resultSender, imageProcessingService, settings);
    }

    private ProcessImageRequest CreateRequest(string fileName, byte[]? contents = null)
    {
        var filePath = Path.Combine(_directory, fileName);
        File.WriteAllBytes(filePath, contents ?? [1, 2, 3]);

        return new ProcessImageRequest
        {
            DownloadedFileInfo = new DownloadedFileInfo(fileName, "/" + fileName, DateTime.UtcNow, "id:" + fileName),
            FileName = filePath,
            Sizes = [256],
            UserId = 1,
            PhotoSourceId = 1,
            PhotoSourceName = "Dropbox",
            RequestId = Guid.NewGuid()
        };
    }

    private static async Task<List<PhotoProcessed>> ReadAsync(IMessageReceiver<PhotoProcessed> receiver, int count,
        TimeSpan? timeout = null)
    {
        using var cancellationTokenSource = new CancellationTokenSource(timeout ?? Timeout);

        var received = new List<PhotoProcessed>();

        while (received.Count < count)
        {
            received.AddRange(await receiver.ReceiveBatchAsync(count - received.Count, TimeSpan.FromMilliseconds(10),
                cancellationTokenSource.Token));
        }

        return received;
    }

    /// <summary>
    /// Fails the first sends, as a broker that is down, then hands the results over.
    /// </summary>
    private class FlakySender(IMessageSender<PhotoProcessed> inner, int failures) : IMessageSender<PhotoProcessed>
    {
        private int _attempts;

        public int Attempts => _attempts;

        public Task SendAsync(PhotoProcessed message, CancellationToken cancellationToken)
        {
            return Interlocked.Increment(ref _attempts) <= failures
                ? Task.FromException(new MessagePublishException("Broker unavailable"))
                : inner.SendAsync(message, cancellationToken);
        }
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
                ThumbnailFiles = new Dictionary<int, string>()
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
