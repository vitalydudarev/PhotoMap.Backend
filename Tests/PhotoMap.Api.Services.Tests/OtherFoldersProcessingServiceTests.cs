using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
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
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services.Tests;

public class OtherFoldersProcessingServiceTests
{
    private const long UserId = 1;
    private const long DropboxSourceId = 1;
    private const long YandexDiskSourceId = 2;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Mock<IUserPhotoSourceService> _userPhotoSourceService = new();
    private readonly Mock<IPhotoSourceService> _photoSourceService = new();
    private readonly Mock<IBackgroundTaskManager> _backgroundTaskManager = new();
    private readonly Mock<IPhotoService> _photoService = new();
    private readonly Mock<IFileStorage> _fileStorage = new();
    private readonly Mock<IFrontendNotificationService> _frontendNotificationService = new();
    private readonly ChannelMessageQueue<ProcessImageRequest> _requestQueue = new();
    private readonly List<UserPhotoSourceStatus> _reportedStatuses = [];
    private Func<CancellationToken, Task>? _startedRun;

    public OtherFoldersProcessingServiceTests()
    {
        _photoSourceService
            .Setup(a => a.GetByIdAsync(DropboxSourceId))
            .ReturnsAsync(CreatePhotoSource(DropboxSourceId, "Dropbox", typeof(DropboxDownloadServiceFactory)));
        _photoSourceService
            .Setup(a => a.GetByIdAsync(YandexDiskSourceId))
            .ReturnsAsync(CreatePhotoSource(YandexDiskSourceId, "Yandex.Disk", typeof(YandexDiskDownloadServiceFactory)));
        _userPhotoSourceService
            .Setup(a => a.GetAuthResultAsync(UserId, It.IsAny<long>()))
            .ReturnsAsync(new UserAuthResult { Token = "token", TokenExpiresOn = DateTimeOffset.UtcNow.AddDays(1) });
        _userPhotoSourceService
            .Setup(a => a.UpdateOtherFoldersStatusAsync(It.IsAny<UserPhotoSourceStatus>()))
            .Callback<UserPhotoSourceStatus>(_reportedStatuses.Add)
            .Returns(Task.CompletedTask);
        _backgroundTaskManager
            .Setup(a => a.TryStartTask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()))
            .Callback<string, Func<CancellationToken, Task>>((_, run) => _startedRun = run)
            .Returns(true);
        _fileStorage
            .Setup(a => a.SaveAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
            .ReturnsAsync((string fileName, byte[] _) => "/storage/" + fileName);
    }

    [Fact]
    public async Task SupportsOtherFoldersAsync_ShouldOnlySupportYandexDisk()
    {
        var service = CreateService(new FakeDiskHandler([]));

        Assert.True(await service.SupportsOtherFoldersAsync(YandexDiskSourceId));
        Assert.False(await service.SupportsOtherFoldersAsync(DropboxSourceId));
    }

    [Fact]
    public async Task RunCommandAsync_ShouldStartNothing_ForASourceOtherThanYandexDisk()
    {
        // Arrange
        var service = CreateService(new FakeDiskHandler([]));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunCommandAsync(UserId, DropboxSourceId, PhotoSourceProcessingCommands.Start));

        _backgroundTaskManager.Verify(a => a.TryStartTask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()),
            Times.Never);
    }

    [Fact]
    public async Task RunCommandAsync_ShouldNotStart_WhileTheOtherFoldersAreBeingProcessed()
    {
        // Arrange
        _backgroundTaskManager.Setup(a => a.IsRunning(It.IsAny<string>())).Returns(true);

        var service = CreateService(new FakeDiskHandler([]));

        // Act
        var accepted = await service.RunCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Start);

        // Assert
        Assert.False(accepted);
    }

    [Fact]
    public async Task Run_ShouldProcessTheOriginalsOfThePhotosOutsideThePhotostream_SkippingTheOnesSavedAlready()
    {
        // Arrange
        var handler = new FakeDiskHandler(
        [
            CreateResource("in-photostream", "disk:/Camera Uploads/a.jpg"),
            CreateResource("in-subfolder", "disk:/Camera Uploads/2020/b.jpg"),
            CreateResource("heic", "disk:/Photos/c.heic", exif: new { date_time = "2019-06-01T10:00:00+00:00" }),
            CreateResource("saved", "disk:/Photos/saved.jpg"),
            CreateResource("unsupported", "disk:/Photos/d.svg")
        ]);

        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, YandexDiskSourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string> { "saved" });

        var requests = new List<ProcessImageRequest>();
        var service = CreateService(handler);

        // Act
        await service.RunCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Start);

        await RunWithWorkerAsync(requests);

        // Assert
        Assert.Equal(["heic", "in-subfolder"], requests.Select(a => a.DownloadedFileInfo.FileId).Order());
        Assert.All(requests, a => Assert.True(a.DownloadedFileInfo.FromOtherFolder));
        Assert.All(requests, a => Assert.StartsWith("/storage/Bin/1/2/", a.FileName));
        Assert.Equal(["disk:/Camera Uploads/2020/b.jpg", "disk:/Photos/c.heic"], handler.DownloadedFiles.Order());

        var heic = requests.Single(a => a.DownloadedFileInfo.FileId == "heic").DownloadedFileInfo;
        Assert.Equal("disk:/Photos/c.heic", heic.Path);
        Assert.Equal(new DateTime(2019, 6, 1, 10, 0, 0, DateTimeKind.Utc), heic.CreatedOn!.Value.ToUniversalTime());

        var lastStatus = _reportedStatuses.Last();
        Assert.Equal(PhotoSourceStatus.Done, lastStatus.Status);
        Assert.Equal(3, lastStatus.TotalCount);
        Assert.Equal(3, lastStatus.ProcessedCount);
        Assert.Equal(0, lastStatus.FailedCount);
    }

    [Fact]
    public async Task Run_ShouldCountAPhotoFailed_WhenItFailsProcessing()
    {
        // Arrange
        var handler = new FakeDiskHandler([CreateResource("broken", "disk:/Photos/broken.jpg")]);

        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, YandexDiskSourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string>());

        var service = CreateService(handler);

        // Act
        await service.RunCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Start);

        await RunWithWorkerAsync([], ProcessingResult.Failed("Failed to decode the image."));

        // Assert
        var lastStatus = _reportedStatuses.Last();
        Assert.Equal(PhotoSourceStatus.Done, lastStatus.Status);
        Assert.Equal(0, lastStatus.ProcessedCount);
        Assert.Equal(1, lastStatus.FailedCount);
    }

    [Fact]
    public void CreateFileInfo_ShouldTakeTheUploadTime_WhenYandexDiskHasNoTimeTaken()
    {
        // Arrange
        var resource = new Resource
        {
            Name = "a.jpg",
            Path = "disk:/Photos/a.jpg",
            ResourceId = "id",
            Type = "file",
            Created = new DateTime(2021, 3, 4, 5, 6, 7, DateTimeKind.Utc)
        };

        // Act
        var fileInfo = OtherFoldersProcessingService.CreateFileInfo(resource);

        // Assert
        Assert.Equal(resource.Created, fileInfo.CreatedOn);
        Assert.True(fileInfo.FromOtherFolder);
    }

    /// <summary>
    /// Runs the run started, with a stand-in for the worker and the saving of the photos: each request is reported
    /// processed with the result given, success unless told otherwise. The run waits for every photo it hands over,
    /// so once it has finished, all of them have been.
    /// </summary>
    private async Task RunWithWorkerAsync(List<ProcessImageRequest> requests, ProcessingResult? result = null)
    {
        using var workerCancellation = new CancellationTokenSource();

        var worker = Task.Run(async () =>
        {
            await foreach (var request in _requestQueue.ReadAllAsync(workerCancellation.Token))
            {
                requests.Add(request);
                request.Processed!.TrySetResult(result ?? ProcessingResult.Success);
            }
        });

        await _startedRun!(CancellationToken.None).WaitAsync(Timeout);

        await workerCancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
    }

    private OtherFoldersProcessingService CreateService(HttpMessageHandler handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(a => a.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));

        var services = new ServiceCollection();
        services.AddSingleton(_userPhotoSourceService.Object);
        services.AddSingleton(_photoService.Object);
        services.AddSingleton(_fileStorage.Object);
        services.AddSingleton(_frontendNotificationService.Object);
        services.AddSingleton<IMessageQueue<ProcessImageRequest>>(_requestQueue);
        services.AddSingleton(httpClientFactory.Object);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return new OtherFoldersProcessingService(_userPhotoSourceService.Object, _photoSourceService.Object,
            _backgroundTaskManager.Object, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new PhotoProcessingSettings { Sizes = [256, 640] }));
    }

    private static object CreateResource(string resourceId, string path, object? exif = null)
    {
        return new
        {
            name = path[(path.LastIndexOf('/') + 1)..],
            path,
            type = "file",
            media_type = "image",
            resource_id = resourceId,
            created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            exif
        };
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

    /// <summary>
    /// A disk whose Photostream is <c>disk:/Camera Uploads/</c>, with the images given on the first page of the flat
    /// list of files, and the files themselves.
    /// </summary>
    private sealed class FakeDiskHandler(object[] images) : HttpMessageHandler
    {
        private readonly List<string> _downloadedFiles = [];

        /// <summary>The paths of the files downloaded.</summary>
        public IReadOnlyList<string> DownloadedFiles
        {
            get
            {
                lock (_downloadedFiles)
                {
                    return _downloadedFiles.ToList();
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;

            if (uri.Host == "downloader.test")
            {
                lock (_downloadedFiles)
                {
                    _downloadedFiles.Add(HttpUtility.ParseQueryString(uri.Query)["path"]!);
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
            }

            if (uri.AbsolutePath.EndsWith("/resources/download"))
            {
                var path = HttpUtility.ParseQueryString(uri.Query)["path"]!;

                return Json(new { href = $"https://downloader.test/file?path={HttpUtility.UrlEncode(path)}", method = "GET" });
            }

            if (uri.AbsolutePath.EndsWith("/resources/files"))
            {
                var query = HttpUtility.ParseQueryString(uri.Query);
                var offset = int.Parse(query["offset"]!);

                Assert.Equal("image", query["media_type"]);

                return Json(new { items = offset == 0 ? images : [], offset, limit = 500 });
            }

            return Json(new { system_folders = new { photostream = "disk:/Camera Uploads/" } });
        }

        private static Task<HttpResponseMessage> Json(object value)
        {
            var content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
