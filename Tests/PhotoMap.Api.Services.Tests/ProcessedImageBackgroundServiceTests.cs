using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Events;
using PhotoMap.Shared.Messages;
using PhotoMap.Shared.Messaging;
using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Tests;

public class ProcessedImageBackgroundServiceTests : IDisposable
{
    private const long UserId = 1;
    private const long SourceId = 2;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string _directory = Directory.CreateTempSubdirectory(nameof(ProcessedImageBackgroundServiceTests)).FullName;
    private readonly Mock<IPhotoService> _photoService = new();
    private readonly Mock<IImageStore> _imageStore = new();
    private readonly ProcessingRequests _processingRequests = new();
    private readonly Dictionary<string, Task<ProcessingResult>> _results = [];
    private readonly List<(IReadOnlyCollection<Photo> Photos, IReadOnlyCollection<OutboxMessage> OutboxMessages)> _saves = [];
    private readonly List<byte[]> _storedThumbnails = [];
    private readonly KafkaSettings _kafkaSettings = new() { Enabled = true };
    private readonly OutboxSignal _outboxSignal = new();

    public ProcessedImageBackgroundServiceTests()
    {
        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, SourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string>());

        _imageStore
            .Setup(a => a.SaveThumbnailAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Callback<byte[], string, string?, string, string, int>((bytes, _, _, _, _, _) =>
            {
                lock (_storedThumbnails)
                {
                    _storedThumbnails.Add(bytes);
                }
            })
            .ReturnsAsync("thumb.jpg");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSaveThePhotosOfABatchWithOneInsert()
    {
        // Arrange
        SetUpSave();
        var messages = Enumerable.Range(0, 3).Select(a => CreateMessage($"id:{a}")).ToList();

        // Act
        await RunAsync(messages);

        // Assert
        var batch = Assert.Single(_saves).Photos;
        Assert.Equal(["id:0", "id:1", "id:2"], batch.Select(a => a.ExternalId));
        Assert.All(_results.Values, a => Assert.True(a.Result.Succeeded));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotSaveThePhotosSavedAlready()
    {
        // Arrange
        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, SourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string> { "id:saved" });
        SetUpSave();

        // Act
        await RunAsync([CreateMessage("id:saved"), CreateMessage("id:new")]);

        // Assert
        var batch = Assert.Single(_saves).Photos;
        Assert.Equal(["id:new"], batch.Select(a => a.ExternalId));
        Assert.All(_results.Values, a => Assert.True(a.Result.Succeeded));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSaveOneAtATime_WhenTheBatchCannotBeSaved()
    {
        // Arrange: the insert of the batch fails on one of its photos, which fails on its own as well
        SetUpSave(photos =>
        {
            if (photos.Any(b => b.ExternalId == "id:bad"))
            {
                throw new InvalidOperationException("Duplicate key");
            }
        });

        // Act
        await RunAsync([CreateMessage("id:0"), CreateMessage("id:bad"), CreateMessage("id:2")]);

        // Assert
        Assert.Equal(["id:0", "id:2"], _saves.Select(a => Assert.Single(a.Photos).ExternalId));
        Assert.True((await _results["id:0"]).Succeeded);
        Assert.False((await _results["id:bad"]).Succeeded);
        Assert.Contains("Duplicate key", (await _results["id:bad"]).Error);
        Assert.True((await _results["id:2"]).Succeeded);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportTheFailure_WhenTheImageCouldNotBeProcessed()
    {
        // Arrange
        SetUpSave();

        // Act
        await RunAsync([CreateFailedMessage("id:corrupt", "Failed to process the image: Unsupported image"), CreateMessage("id:0")]);

        // Assert
        var result = await _results["id:corrupt"];
        Assert.False(result.Succeeded);
        Assert.Equal("Failed to process the image: Unsupported image", result.Error);
        Assert.Equal(["id:0"], Assert.Single(_saves).Photos.Select(a => a.ExternalId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStoreTheThumbnailsFromTheirFiles_AndDeleteTheFiles()
    {
        // Arrange
        SetUpSave();
        var message = CreateMessage("id:0");

        // Act
        await RunAsync([message]);

        // Assert
        // one size: the same thumbnail is the small and the large one
        Assert.Equal([[0xAB], [0xAB]], _storedThumbnails);
        Assert.All(message.Image!.ThumbnailFiles.Values, a => Assert.False(File.Exists(a)));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSaveThePhoto_WhenNothingWaitsForTheResult()
    {
        // Arrange: requested before a restart, the run waiting for it is gone
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetUpSave(_ => saved.TrySetResult());
        var message = CreateMessage("id:0", register: false);

        // Act
        var service = await StartAsync([message]);
        await saved.Task.WaitAsync(Timeout);
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(["id:0"], Assert.Single(_saves).Photos.Select(a => a.ExternalId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAcknowledgeTheMessages_OnceTheyHaveBeenSaved()
    {
        // Arrange
        var steps = new List<string>();
        SetUpSave(_ => steps.Add("saved"));

        var receiver = new Mock<IMessageReceiver<PhotoProcessed>>();
        receiver
            .SetupSequence(a => a.ReceiveBatchAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateMessage("id:0")])
            .ReturnsAsync([]);
        receiver.Setup(a => a.Acknowledge()).Callback(() => steps.Add("acknowledged"));

        // Act
        var service = CreateService(receiver.Object);
        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(Timeout);
        await service.StopAsync(CancellationToken.None);

        // Assert: acknowledged before, the message would be lost when the save fails or the application stops
        Assert.Equal(["saved", "acknowledged"], steps);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSaveAnEventForEachPhotoToTheOutbox_WithThePhotos()
    {
        // Arrange: one photo saved already, which gets no event
        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, SourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string> { "id:saved" });
        SetUpSave();

        // Act
        await RunAsync([CreateMessage("id:saved"), CreateMessage("id:0"), CreateMessage("id:1")]);

        // Assert: in the same call as the photos, so in the same transaction
        var (photos, outboxMessages) = Assert.Single(_saves);
        Assert.Equal(["id:0", "id:1"], photos.Select(a => a.ExternalId));
        Assert.All(outboxMessages, a => Assert.Equal(PhotoSavedEvent.Topic, a.Topic));
        Assert.All(outboxMessages, a => Assert.Equal($"{UserId}:{SourceId}", a.Key));
        Assert.Equal(["id:0", "id:1"],
            outboxMessages.Select(a => JsonSerializer.Deserialize<PhotoSavedEvent>(a.Payload)!.ExternalId));
        Assert.True(_outboxSignal.WaitAsync(CancellationToken.None).IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSaveNoEvents_WhenKafkaIsDisabled()
    {
        // Arrange
        SetUpSave();
        _kafkaSettings.Enabled = false;

        // Act
        await RunAsync([CreateMessage("id:0")]);

        // Assert
        var (photos, outboxMessages) = Assert.Single(_saves);
        Assert.Single(photos);
        Assert.Empty(outboxMessages);
        Assert.False(_outboxSignal.WaitAsync(CancellationToken.None).IsCompleted);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private void SetUpSave(Action<IReadOnlyCollection<Photo>>? onSave = null)
    {
        _photoService
            .Setup(a => a.AddRangeAsync(It.IsAny<IReadOnlyCollection<Photo>>(),
                It.IsAny<IReadOnlyCollection<OutboxMessage>?>()))
            .Callback<IReadOnlyCollection<Photo>, IReadOnlyCollection<OutboxMessage>?>((photos, outboxMessages) =>
            {
                onSave?.Invoke(photos);
                _saves.Add((photos, outboxMessages ?? []));
            })
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Sends the messages before the service starts, so that they make one batch, and waits until the run would
    /// have been told the result of each.
    /// </summary>
    private async Task RunAsync(IReadOnlyCollection<PhotoProcessed> messages)
    {
        var service = await StartAsync(messages);

        await Task.WhenAll(_results.Values).WaitAsync(Timeout);
        await service.StopAsync(CancellationToken.None);
    }

    private async Task<ProcessedImageBackgroundService> StartAsync(IReadOnlyCollection<PhotoProcessed> messages)
    {
        var transport = new ChannelMessageTransport<PhotoProcessed>();

        foreach (var message in messages)
        {
            await transport.SendAsync(message, CancellationToken.None);
        }

        var service = CreateService(transport);
        await service.StartAsync(CancellationToken.None);

        return service;
    }

    private ProcessedImageBackgroundService CreateService(IMessageReceiver<PhotoProcessed> receiver)
    {
        return new ProcessedImageBackgroundService(receiver, _processingRequests, CreateServiceScopeFactory(),
            NullLogger<ProcessedImageBackgroundService>.Instance,
            Options.Create(new PhotoProcessingSettings
            {
                Sizes = [256],
                SaveBatchSize = 10,
                SaveBatchMaxWait = TimeSpan.FromMilliseconds(50)
            }),
            _kafkaSettings,
            _outboxSignal);
    }

    private IServiceScopeFactory CreateServiceScopeFactory()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(a => a.GetService(typeof(IPhotoService))).Returns(_photoService.Object);
        serviceProvider.Setup(a => a.GetService(typeof(IImageStore))).Returns(_imageStore.Object);

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(a => a.ServiceProvider).Returns(serviceProvider.Object);

        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        serviceScopeFactory.Setup(a => a.CreateScope()).Returns(serviceScope.Object);

        return serviceScopeFactory.Object;
    }

    /// <summary>
    /// A processed image, its thumbnail left in a file as the worker does, and its request registered as a run
    /// does, unless told otherwise; <see cref="_results"/> gets what the run is told.
    /// </summary>
    private PhotoProcessed CreateMessage(string externalId, bool register = true)
    {
        var thumbnailFile = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".256.jpg");
        File.WriteAllBytes(thumbnailFile, [0xAB]);

        return new PhotoProcessed
        {
            RequestId = register ? Register(externalId) : Guid.NewGuid(),
            UserId = UserId,
            PhotoSourceId = SourceId,
            FileName = externalId + ".jpg",
            Image = new ProcessedImage
            {
                FileName = externalId + ".jpg",
                UserId = UserId,
                PhotoSourceId = SourceId,
                PhotoSourceName = "Dropbox",
                Path = "/" + externalId + ".jpg",
                ExternalId = externalId,
                ContentHash = externalId,
                ThumbnailFiles = new Dictionary<int, string> { [256] = thumbnailFile }
            }
        };
    }

    private PhotoProcessed CreateFailedMessage(string externalId, string error)
    {
        return new PhotoProcessed
        {
            RequestId = Register(externalId),
            UserId = UserId,
            PhotoSourceId = SourceId,
            FileName = externalId + ".jpg",
            Error = error
        };
    }

    private Guid Register(string externalId)
    {
        var processed = new TaskCompletionSource<ProcessingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _results[externalId] = processed.Task;

        return _processingRequests.Register(processed);
    }
}
