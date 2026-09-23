using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Queues;

namespace PhotoMap.Api.Services.Tests;

public class PhotoSourceProcessingServiceTests
{
    private const long UserId = 1;
    private const long SourceId = 1;

    private readonly Mock<IUserPhotoSourceService> _userPhotoSourceService = new();
    private readonly List<UserPhotoSourceStatus> _reportedStatuses = [];

    [Fact]
    public async Task RunCommandAsync_ShouldContinueTheCountersOfAStoppedRun()
    {
        // Arrange: a run that was stopped in the middle of a page, so it saved no cursor to continue from
        SetUpUserPhotoSource(processedCount: 39, failedCount: 1, state: null);

        var service = CreateService(out var runTask);

        // Act
        await service.RunCommandAsync(UserId, SourceId, PhotoSourceProcessingCommands.Start);
        await runTask();

        // Assert: the run carries on counting from the file it had reached
        Assert.NotEmpty(_reportedStatuses);
        Assert.All(_reportedStatuses, a => Assert.Equal(39, a.ProcessedCount));
        Assert.All(_reportedStatuses, a => Assert.Equal(1, a.FailedCount));
    }

    [Fact]
    public async Task RunCommandAsync_ShouldStartCountingFromZero_WhenTheSourceWasNeverProcessed()
    {
        // Arrange
        SetUpUserPhotoSource(processedCount: 0, failedCount: 0, state: null);

        var service = CreateService(out var runTask);

        // Act
        await service.RunCommandAsync(UserId, SourceId, PhotoSourceProcessingCommands.Start);
        await runTask();

        // Assert
        Assert.NotEmpty(_reportedStatuses);
        Assert.All(_reportedStatuses, a => Assert.Equal(0, a.ProcessedCount));
    }

    private void SetUpUserPhotoSource(int processedCount, int failedCount, string? state)
    {
        _userPhotoSourceService
            .Setup(a => a.GetAuthResultAsync(UserId, SourceId))
            .ReturnsAsync(new UserAuthResult { Token = "token", TokenExpiresOn = DateTimeOffset.UtcNow.AddHours(1) });

        _userPhotoSourceService
            .Setup(a => a.GetUserPhotoStateAsync(UserId, SourceId))
            .ReturnsAsync(new UserPhotoSourceState { UserId = UserId, PhotoSourceId = SourceId, State = state });

        _userPhotoSourceService
            .Setup(a => a.GetUserPhotoStatusAsync(UserId, SourceId))
            .ReturnsAsync(new UserPhotoSourceStatus
            {
                UserId = UserId,
                PhotoSourceId = SourceId,
                Status = PhotoSourceStatus.Stopped,
                TotalCount = 2826,
                ProcessedCount = processedCount,
                FailedCount = failedCount,
                LastUpdatedAt = DateTimeOffset.UtcNow
            });

        _userPhotoSourceService
            .Setup(a => a.UpdateUserPhotoStatusAsync(It.IsAny<UserPhotoSourceStatus>()))
            .Callback<UserPhotoSourceStatus>(_reportedStatuses.Add)
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// The service started by the returned function downloads nothing, the run only reports its counters.
    /// </summary>
    private PhotoSourceProcessingService CreateService(out Func<Task> runTask)
    {
        var photoSource = CreatePhotoSource();

        var photoSourceService = new Mock<IPhotoSourceService>();
        photoSourceService.Setup(a => a.GetByIdAsync(SourceId)).ReturnsAsync(photoSource);

        var downloadService = new Mock<IDownloadService>();
        downloadService.Setup(a => a.GetTotalFileCountAsync()).ReturnsAsync(2826);
        downloadService.Setup(a => a.DownloadAsync(It.IsAny<CancellationToken>())).Returns(NoFilesAsync());

        var downloadServiceFactory = new Mock<IPhotoSourceDownloadServiceFactory>();
        downloadServiceFactory
            .Setup(a => a.GetService(It.IsAny<PhotoSource>(), It.IsAny<DownloadServiceParameters>()))
            .Returns(downloadService.Object);

        Task? run = null;

        var backgroundTaskManager = new Mock<IBackgroundTaskManager>();
        backgroundTaskManager
            .Setup(a => a.TryStartTask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()))
            .Callback<string, Func<CancellationToken, Task>>((_, task) => run = task(CancellationToken.None))
            .Returns(true);

        runTask = () => run ?? Task.CompletedTask;

        return new PhotoSourceProcessingService(_userPhotoSourceService.Object, photoSourceService.Object,
            backgroundTaskManager.Object, CreateServiceScopeFactory(downloadServiceFactory.Object),
            Options.Create(new PhotoProcessingSettings { Sizes = [256] }));
    }

    private IServiceScopeFactory CreateServiceScopeFactory(IPhotoSourceDownloadServiceFactory downloadServiceFactory)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(a => a.GetService(typeof(IMessageQueue<ProcessImageRequest>)))
            .Returns(new ChannelMessageQueue<ProcessImageRequest>());
        serviceProvider.Setup(a => a.GetService(typeof(IFileStorage))).Returns(new Mock<IFileStorage>().Object);
        serviceProvider.Setup(a => a.GetService(typeof(IFrontendNotificationService)))
            .Returns(new Mock<IFrontendNotificationService>().Object);
        serviceProvider.Setup(a => a.GetService(typeof(ILogger<PhotoSourceProcessingService>)))
            .Returns(NullLogger<PhotoSourceProcessingService>.Instance);
        serviceProvider.Setup(a => a.GetService(typeof(IPhotoSourceDownloadServiceFactory))).Returns(downloadServiceFactory);
        serviceProvider.Setup(a => a.GetService(typeof(IUserPhotoSourceService))).Returns(_userPhotoSourceService.Object);

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(a => a.ServiceProvider).Returns(serviceProvider.Object);

        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        serviceScopeFactory.Setup(a => a.CreateScope()).Returns(serviceScope.Object);

        return serviceScopeFactory.Object;
    }

    private static async IAsyncEnumerable<DownloadedFile> NoFilesAsync()
    {
        await Task.CompletedTask;

        yield break;
    }

    private static PhotoSource CreatePhotoSource()
    {
        return new PhotoSource
        {
            Id = SourceId,
            Name = "Dropbox",
            ServiceSettings = "Settings",
            ClientAuthSettings = new ClientAuthSettings
            {
                OAuthConfiguration = new OAuthConfiguration
                {
                    ClientId = "client-id",
                    RedirectUri = "",
                    ResponseType = "",
                    AuthorizeUrl = ""
                },
                RelativeAuthUrl = "/"
            },
            ServiceFactoryImplementationType = "Factory"
        };
    }
}
