using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Queues;

namespace PhotoMap.Api.Services.Tests;

public class ProcessedImageBackgroundServiceTests
{
    private const long UserId = 1;
    private const long SourceId = 2;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Mock<IPhotoService> _photoService = new();
    private readonly List<IReadOnlyCollection<Photo>> _savedBatches = [];

    public ProcessedImageBackgroundServiceTests()
    {
        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, SourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSaveThePhotosOfABatchWithOneInsert()
    {
        // Arrange
        _photoService
            .Setup(a => a.AddRangeAsync(It.IsAny<IReadOnlyCollection<Photo>>()))
            .Callback<IReadOnlyCollection<Photo>>(_savedBatches.Add)
            .Returns(Task.CompletedTask);

        var queue = new ChannelMessageQueue<ProcessedImage>();
        var images = Enumerable.Range(0, 3).Select(a => CreateImage($"id:{a}")).ToList();

        // Act
        await RunAsync(queue, images);

        // Assert
        var batch = Assert.Single(_savedBatches);
        Assert.Equal(["id:0", "id:1", "id:2"], batch.Select(a => a.ExternalId));
        Assert.All(images, a => Assert.True(a.Processed!.Task.Result.Succeeded));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotSaveThePhotosSavedAlready()
    {
        // Arrange
        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, SourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string> { "id:saved" });
        _photoService
            .Setup(a => a.AddRangeAsync(It.IsAny<IReadOnlyCollection<Photo>>()))
            .Callback<IReadOnlyCollection<Photo>>(_savedBatches.Add)
            .Returns(Task.CompletedTask);

        var queue = new ChannelMessageQueue<ProcessedImage>();
        var images = new[] { CreateImage("id:saved"), CreateImage("id:new") };

        // Act
        await RunAsync(queue, images);

        // Assert
        var batch = Assert.Single(_savedBatches);
        Assert.Equal(["id:new"], batch.Select(a => a.ExternalId));
        Assert.All(images, a => Assert.True(a.Processed!.Task.Result.Succeeded));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSaveOneAtATime_WhenTheBatchCannotBeSaved()
    {
        // Arrange: the insert of the batch fails on one of its photos, which fails on its own as well
        _photoService
            .Setup(a => a.AddRangeAsync(It.IsAny<IReadOnlyCollection<Photo>>()))
            .Callback<IReadOnlyCollection<Photo>>(a =>
            {
                if (a.Any(b => b.ExternalId == "id:bad"))
                {
                    throw new InvalidOperationException("Duplicate key");
                }

                _savedBatches.Add(a);
            })
            .Returns(Task.CompletedTask);

        var queue = new ChannelMessageQueue<ProcessedImage>();
        var images = new[] { CreateImage("id:0"), CreateImage("id:bad"), CreateImage("id:2") };

        // Act
        await RunAsync(queue, images);

        // Assert
        Assert.Equal(["id:0", "id:2"], _savedBatches.Select(a => Assert.Single(a).ExternalId));
        Assert.True(images[0].Processed!.Task.Result.Succeeded);
        Assert.False(images[1].Processed!.Task.Result.Succeeded);
        Assert.Contains("Duplicate key", images[1].Processed!.Task.Result.Error);
        Assert.True(images[2].Processed!.Task.Result.Succeeded);
    }

    /// <summary>
    /// Enqueues the images before the service starts, so that they make one batch, and waits until all of them
    /// have been processed.
    /// </summary>
    private async Task RunAsync(ChannelMessageQueue<ProcessedImage> queue, IReadOnlyCollection<ProcessedImage> images)
    {
        foreach (var image in images)
        {
            await queue.EnqueueAsync(image);
        }

        var service = new ProcessedImageBackgroundService(queue, CreateServiceScopeFactory(),
            NullLogger<ProcessedImageBackgroundService>.Instance,
            Options.Create(new PhotoProcessingSettings
            {
                Sizes = [256],
                SaveBatchSize = 10,
                SaveBatchMaxWait = TimeSpan.FromMilliseconds(50)
            }));

        await service.StartAsync(CancellationToken.None);
        await Task.WhenAll(images.Select(a => a.Processed!.Task)).WaitAsync(Timeout);
        await service.StopAsync(CancellationToken.None);
    }

    private IServiceScopeFactory CreateServiceScopeFactory()
    {
        var imageStore = new Mock<IImageStore>();
        imageStore
            .Setup(a => a.SaveThumbnailAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync("thumb.jpg");

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(a => a.GetService(typeof(IPhotoService))).Returns(_photoService.Object);
        serviceProvider.Setup(a => a.GetService(typeof(IImageStore))).Returns(imageStore.Object);

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(a => a.ServiceProvider).Returns(serviceProvider.Object);

        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        serviceScopeFactory.Setup(a => a.CreateScope()).Returns(serviceScope.Object);

        return serviceScopeFactory.Object;
    }

    private static ProcessedImage CreateImage(string externalId)
    {
        return new ProcessedImage
        {
            FileName = externalId + ".jpg",
            UserId = UserId,
            PhotoSourceId = SourceId,
            PhotoSourceName = "Dropbox",
            Path = "/" + externalId + ".jpg",
            ExternalId = externalId,
            ContentHash = externalId,
            Thumbs = new Dictionary<int, byte[]> { [256] = [1] },
            Processed = new TaskCompletionSource<ProcessingResult>(TaskCreationOptions.RunContinuationsAsynchronously)
        };
    }
}
