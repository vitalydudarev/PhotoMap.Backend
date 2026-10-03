using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services.Tests;

public class VideoProcessingServiceTests
{
    private const long UserId = 1;
    private const long DropboxSourceId = 1;
    private const long YandexDiskSourceId = 2;

    private readonly Mock<IUserPhotoSourceService> _userPhotoSourceService = new();
    private readonly Mock<IPhotoSourceService> _photoSourceService = new();
    private readonly Mock<IVideoService> _videoService = new();
    private readonly Mock<IBackgroundTaskManager> _backgroundTaskManager = new();
    private readonly Mock<IFileStorage> _fileStorage = new();
    private readonly Mock<IFrontendNotificationService> _frontendNotificationService = new();

    public VideoProcessingServiceTests()
    {
        _photoSourceService
            .Setup(a => a.GetByIdAsync(DropboxSourceId))
            .ReturnsAsync(CreatePhotoSource(DropboxSourceId, "Dropbox", typeof(DropboxDownloadServiceFactory)));
        _photoSourceService
            .Setup(a => a.GetByIdAsync(YandexDiskSourceId))
            .ReturnsAsync(CreatePhotoSource(YandexDiskSourceId, "Yandex.Disk", typeof(YandexDiskDownloadServiceFactory)));
        _videoService
            .Setup(a => a.DeleteByPhotoSourceAsync(UserId, YandexDiskSourceId))
            .ReturnsAsync(["Yandex.Disk/1/video-previews/video_1.jpg", "Yandex.Disk/1/video-previews/video_2.jpg"]);
    }

    [Fact]
    public async Task SupportsVideosAsync_ShouldOnlySupportYandexDisk()
    {
        var service = CreateService();

        Assert.True(await service.SupportsVideosAsync(YandexDiskSourceId));
        Assert.False(await service.SupportsVideosAsync(DropboxSourceId));
    }

    [Fact]
    public async Task DeleteDataAsync_ShouldDeleteTheVideosPreviewsAndStatus()
    {
        // Arrange
        var service = CreateService();

        // Act
        var deleted = await service.DeleteDataAsync(UserId, YandexDiskSourceId);

        // Assert
        Assert.True(deleted);

        _videoService.Verify(a => a.DeleteByPhotoSourceAsync(UserId, YandexDiskSourceId));
        _fileStorage.Verify(a => a.Delete("Yandex.Disk/1/video-previews/video_1.jpg"));
        _fileStorage.Verify(a => a.Delete("Yandex.Disk/1/video-previews/video_2.jpg"));
        _videoService.Verify(a => a.DeleteStatusAsync(UserId, YandexDiskSourceId));
        _frontendNotificationService.Verify(a => a.SendVideoProgressAsync(It.Is<UserPhotoSourceStatus>(b =>
            b.PhotoSourceId == YandexDiskSourceId && b.Status == PhotoSourceStatus.NotStarted)));
    }

    [Fact]
    public async Task DeleteDataAsync_ShouldDeleteNothing_WhileTheVideosAreBeingProcessed()
    {
        // Arrange
        _backgroundTaskManager.Setup(a => a.IsRunning(It.IsAny<string>())).Returns(true);

        var service = CreateService();

        // Act
        var deleted = await service.DeleteDataAsync(UserId, YandexDiskSourceId);

        // Assert
        Assert.False(deleted);

        _videoService.VerifyNoOtherCalls();
        _fileStorage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteDataAsync_ShouldDeleteTheRest_WhenAPreviewCannotBeDeleted()
    {
        // Arrange
        _fileStorage.Setup(a => a.Delete("Yandex.Disk/1/video-previews/video_1.jpg")).Throws<IOException>();

        var service = CreateService();

        // Act
        var deleted = await service.DeleteDataAsync(UserId, YandexDiskSourceId);

        // Assert
        Assert.True(deleted);

        _fileStorage.Verify(a => a.Delete("Yandex.Disk/1/video-previews/video_2.jpg"));
        _videoService.Verify(a => a.DeleteStatusAsync(UserId, YandexDiskSourceId));
    }

    [Fact]
    public async Task RunCommandAsync_ShouldStartNothing_ForASourceOtherThanYandexDisk()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunCommandAsync(UserId, DropboxSourceId, PhotoSourceProcessingCommands.Start));

        _backgroundTaskManager.Verify(a => a.TryStartTask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()),
            Times.Never);
    }

    [Fact]
    public async Task RunCommandAsync_ShouldNotStartTheVideos_WhileTheyAreBeingProcessed()
    {
        // Arrange
        _backgroundTaskManager.Setup(a => a.IsRunning(It.IsAny<string>())).Returns(true);

        var service = CreateService();

        // Act
        var accepted = await service.RunCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Start);

        // Assert
        Assert.False(accepted);
    }

    [Fact]
    public void GetPreviewUrl_ShouldTakeTheXxxlSize()
    {
        var resource = new Resource
        {
            Sizes =
            [
                new ResourceSize { Name = "ORIGINAL", Url = "https://downloader.disk.yandex.ru/preview/original" },
                new ResourceSize { Name = "XXXL", Url = "https://downloader.disk.yandex.ru/preview/xxxl" },
                new ResourceSize { Name = "XL", Url = "https://downloader.disk.yandex.ru/preview/xl" }
            ]
        };

        Assert.Equal("https://downloader.disk.yandex.ru/preview/xxxl", YandexDiskVideoSource.GetPreviewUrl(resource));
    }

    [Fact]
    public void GetPreviewUrl_ShouldTakeTheLargestSize_WhenThereIsNoXxxlSize()
    {
        var resource = new Resource
        {
            Sizes =
            [
                new ResourceSize { Name = "S", Url = "https://downloader.disk.yandex.ru/preview/s" },
                new ResourceSize { Name = "XL", Url = "https://downloader.disk.yandex.ru/preview/xl" },
                new ResourceSize { Name = "DEFAULT", Url = "https://downloader.disk.yandex.ru/preview/default" }
            ]
        };

        Assert.Equal("https://downloader.disk.yandex.ru/preview/xl", YandexDiskVideoSource.GetPreviewUrl(resource));
    }

    [Fact]
    public void GetPreviewUrl_ShouldTakeTheCSize_WhenItIsTheOnlyOne()
    {
        var resource = new Resource
        {
            Sizes = [new ResourceSize { Name = "C", Url = "https://downloader.disk.yandex.ru/preview/c" }]
        };

        Assert.Equal("https://downloader.disk.yandex.ru/preview/c", YandexDiskVideoSource.GetPreviewUrl(resource));
    }

    [Fact]
    public void GetPreviewUrl_ShouldBeNull_WhenThereIsNoPreviewOfAnyOfTheSizes()
    {
        var resource = new Resource
        {
            Sizes = [new ResourceSize { Name = "ORIGINAL", Url = "https://downloader.disk.yandex.ru/preview/original" }]
        };

        Assert.Null(YandexDiskVideoSource.GetPreviewUrl(resource));
    }

    [Theory]
    [InlineData("disk:/Camera Uploads/video.mp4", "disk:/Camera Uploads")]
    [InlineData("disk:/Videos/2024/Trip/video.mp4", "disk:/Videos/2024/Trip")]
    [InlineData("disk:/video.mp4", "disk:/")]
    [InlineData(null, null)]
    public void GetFolderPath_ShouldLeaveTheFileNameOut(string? path, string? expectedFolderPath)
    {
        Assert.Equal(expectedFolderPath, VideoProcessingService.GetFolderPath(path));
    }

    private VideoProcessingService CreateService()
    {
        return new VideoProcessingService(_userPhotoSourceService.Object, _photoSourceService.Object, _videoService.Object,
            _backgroundTaskManager.Object, new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            _fileStorage.Object, _frontendNotificationService.Object, NullLogger<VideoProcessingService>.Instance);
    }

    private static PhotoSource CreatePhotoSource(long id, string name, Type serviceFactoryType)
    {
        return new PhotoSource
        {
            Id = id,
            Name = name,
            ServiceSettings = "{}",
            ClientAuthSettings = new ClientAuthSettings
            {
                OAuthConfiguration = new OAuthConfiguration
                {
                    ClientId = "",
                    RedirectUri = "",
                    ResponseType = "",
                    AuthorizeUrl = ""
                },
                RelativeAuthUrl = "/"
            },
            ServiceFactoryImplementationType = TypeHelper.GetTypeFullName(serviceFactoryType)
        };
    }
}
