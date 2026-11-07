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

public class PhotoSourceProcessingServiceOtherUploadsTests
{
    private const long UserId = 1;
    private const long DropboxSourceId = 1;
    private const long YandexDiskSourceId = 2;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Mock<IUserPhotoSourceService> _userPhotoSourceService = new();
    private readonly Mock<IPhotoSourceService> _photoSourceService = new();
    private readonly Mock<IBackgroundTaskManager> _backgroundTaskManager = new();
    private readonly Mock<IPhotoService> _photoService = new();
    private readonly Mock<IFailedFileService> _failedFileService = new();
    private readonly Mock<IFileStorage> _fileStorage = new();
    private readonly Mock<IFrontendNotificationService> _frontendNotificationService = new();
    private readonly ChannelMessageQueue<ProcessImageRequest> _requestQueue = new();
    private readonly ProcessingRequests _processingRequests = new();
    private readonly List<UserPhotoSourceStatus> _reportedStatuses = [];
    private Func<CancellationToken, Task>? _startedRun;

    /// <summary>The other uploads recorded as failed, as the database keeps them: each once.</summary>
    private readonly HashSet<string> _recordedFailures = [];

    public PhotoSourceProcessingServiceOtherUploadsTests()
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
            .Setup(a => a.UpdateOtherUploadsStatusAsync(It.IsAny<UserPhotoSourceStatus>()))
            .Callback<UserPhotoSourceStatus>(_reportedStatuses.Add)
            .Returns(Task.CompletedTask);
        _backgroundTaskManager
            .Setup(a => a.TryStartTask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()))
            .Callback<string, Func<CancellationToken, Task>>((_, run) => _startedRun = run)
            .Returns(true);
        _fileStorage
            .Setup(a => a.SaveAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
            .ReturnsAsync((string fileName, byte[] _) => "/storage/" + fileName);
        _failedFileService
            .Setup(a => a.RecordAsync(UserId, YandexDiskSourceId, It.IsAny<IReadOnlyCollection<FailedFile>>(),
                It.IsAny<IReadOnlyCollection<string>>()))
            .Callback<long, long, IReadOnlyCollection<FailedFile>, IReadOnlyCollection<string>>((_, _, failed, saved) =>
            {
                _recordedFailures.UnionWith(failed.Select(a => a.ExternalId));
                _recordedFailures.ExceptWith(saved);
            })
            .Returns(Task.CompletedTask);
        _failedFileService
            .Setup(a => a.CountAsync(UserId, YandexDiskSourceId, true))
            .ReturnsAsync(() => _recordedFailures.Count);
    }

    [Fact]
    public async Task SupportsOtherUploadsAsync_ShouldOnlySupportYandexDisk()
    {
        var service = CreateService(new FakeDiskHandler([]));

        Assert.True(await service.SupportsOtherUploadsAsync(YandexDiskSourceId));
        Assert.False(await service.SupportsOtherUploadsAsync(DropboxSourceId));
    }

    [Fact]
    public async Task RunOtherUploadsCommandAsync_ShouldStartNothing_ForASourceOtherThanYandexDisk()
    {
        // Arrange
        var service = CreateService(new FakeDiskHandler([]));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunOtherUploadsCommandAsync(UserId, DropboxSourceId, PhotoSourceProcessingCommands.Start));

        _backgroundTaskManager.Verify(a => a.TryStartTask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()),
            Times.Never);
    }

    [Fact]
    public async Task RunOtherUploadsCommandAsync_ShouldNotStart_WhileTheOtherUploadsAreBeingProcessed()
    {
        // Arrange
        _backgroundTaskManager.Setup(a => a.IsRunning(It.IsAny<string>())).Returns(true);

        var service = CreateService(new FakeDiskHandler([]));

        // Act
        var accepted = await service.RunOtherUploadsCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Start);

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
        await service.RunOtherUploadsCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Start);

        await RunWithWorkerAsync(requests);

        // Assert
        Assert.Equal(["heic", "in-subfolder"], requests.Select(a => a.DownloadedFileInfo.FileId).Order());
        Assert.All(requests, a => Assert.True(a.DownloadedFileInfo.FromOtherUploads));
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
    public async Task Run_ShouldLeaveOutThePhotosOfTheФотокамераFolder()
    {
        // Arrange
        var handler = new FakeDiskHandler(
        [
            CreateResource("photo-camera", "disk:/Фотокамера/a.jpg"),
            CreateResource("photo-camera-subfolder", "disk:/Фотокамера/2020/b.jpg"),
            CreateResource("other", "disk:/Photos/c.jpg"),
            CreateResource("similar-folder", "disk:/Фотокамера 2/d.jpg")
        ]);

        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, YandexDiskSourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string>());

        var requests = new List<ProcessImageRequest>();
        var service = CreateService(handler);

        // Act
        await service.RunOtherUploadsCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Start);

        await RunWithWorkerAsync(requests);

        // Assert
        Assert.Equal(["other", "similar-folder"], requests.Select(a => a.DownloadedFileInfo.FileId).Order());
        Assert.Equal(["disk:/Photos/c.jpg", "disk:/Фотокамера 2/d.jpg"], handler.DownloadedFiles.Order());
        Assert.Equal(2, _reportedStatuses.Last().TotalCount);
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
        await service.RunOtherUploadsCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Start);

        await RunWithWorkerAsync([], ProcessingResult.Failed("Failed to decode the image."));

        // Assert
        var lastStatus = _reportedStatuses.Last();
        Assert.Equal(PhotoSourceStatus.Done, lastStatus.Status);
        Assert.Equal(0, lastStatus.ProcessedCount);
        Assert.Equal(1, lastStatus.FailedCount);
    }

    [Fact]
    public async Task Run_ShouldRecordThePhotosThatFail_AndTheOnesSavedAsNoLongerFailed()
    {
        // Arrange
        var handler = new FakeDiskHandler(
        [
            CreateResource("broken", "disk:/Photos/broken.jpg"),
            CreateResource("fine", "disk:/Photos/fine.jpg")
        ]);

        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, YandexDiskSourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string>());

        var service = CreateService(handler);

        // Act
        await service.RunOtherUploadsCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Start);

        await RunWithWorkerAsync([], request => request.DownloadedFileInfo.FileId == "broken"
            ? ProcessingResult.Failed("Failed to decode the image.")
            : ProcessingResult.Success);

        // Assert
        _failedFileService.Verify(a => a.RecordAsync(UserId, YandexDiskSourceId,
            It.Is<IReadOnlyCollection<FailedFile>>(b => b.Count == 1 && b.All(c =>
                c.ExternalId == "broken" && c.Path == "disk:/Photos/broken.jpg" && c.FileName == "broken.jpg" &&
                c.Stage == FileFailureStage.Processing && c.Error == "Failed to decode the image." && c.FromOtherUploads)),
            It.Is<IReadOnlyCollection<string>>(b => b.SequenceEqual(new[] { "fine" }))));

        var lastStatus = _reportedStatuses.Last();
        Assert.Equal(1, lastStatus.ProcessedCount);
        Assert.Equal(1, lastStatus.FailedCount);
    }

    [Fact]
    public async Task Run_ShouldRecordAPhotoThatFailsToDownload_AtTheDownloadStage()
    {
        // Arrange
        var handler = new FakeDiskHandler([CreateResource("missing", "disk:/Photos/missing.jpg")]) { FailDownloads = true };

        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, YandexDiskSourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string>());

        var requests = new List<ProcessImageRequest>();
        var service = CreateService(handler);

        // Act
        await service.RunOtherUploadsCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Start);

        await RunWithWorkerAsync(requests);

        // Assert
        Assert.Empty(requests);
        _failedFileService.Verify(a => a.RecordAsync(UserId, YandexDiskSourceId,
            It.Is<IReadOnlyCollection<FailedFile>>(b => b.Single().ExternalId == "missing" &&
                                                        b.Single().Stage == FileFailureStage.Download &&
                                                        b.Single().FromOtherUploads),
            It.Is<IReadOnlyCollection<string>>(b => b.Count == 0)));
        Assert.Equal(1, _reportedStatuses.Last().FailedCount);
    }

    [Fact]
    public async Task RetryFailed_ShouldRetryOnlyTheFailedPhotos_CarryingOnTheCountersOfTheEarlierRuns()
    {
        // Arrange: the flat list is not listed, the failed photos are looked up by their paths
        var handler = new FakeDiskHandler([],
        [
            CreateResource("recovered", "disk:/Photos/recovered.jpg"),
            CreateResource("still-broken", "disk:/Photos/still-broken.jpg")
        ]);

        _failedFileService
            .Setup(a => a.GetOtherUploadsAsync(UserId, YandexDiskSourceId))
            .ReturnsAsync([
                CreateFailedFile("recovered", "disk:/Photos/recovered.jpg"),
                CreateFailedFile("still-broken", "disk:/Photos/still-broken.jpg")
            ]);
        _recordedFailures.UnionWith(["recovered", "still-broken"]);
        _userPhotoSourceService
            .Setup(a => a.GetOtherUploadsStatusAsync(UserId, YandexDiskSourceId))
            .ReturnsAsync(new UserPhotoSourceStatus
            {
                UserId = UserId,
                PhotoSourceId = YandexDiskSourceId,
                Status = PhotoSourceStatus.Done,
                TotalCount = 10,
                ProcessedCount = 8,
                FailedCount = 2
            });

        var retries = new List<RetryProgress>();
        _frontendNotificationService
            .Setup(a => a.SendOtherUploadsProgressAsync(It.IsAny<UserPhotoSourceStatus>(), It.IsAny<RetryProgress>()))
            .Callback<UserPhotoSourceStatus, RetryProgress>((_, retry) => retries.Add(retry))
            .Returns(Task.CompletedTask);

        var requests = new List<ProcessImageRequest>();
        var service = CreateService(handler);

        // Act
        var accepted = await service.RunOtherUploadsCommandAsync(UserId, YandexDiskSourceId,
            PhotoSourceProcessingCommands.RetryFailed);

        await RunWithWorkerAsync(requests, request => request.DownloadedFileInfo.FileId == "still-broken"
            ? ProcessingResult.Failed("Failed to decode the image.")
            : ProcessingResult.Success);

        // Assert
        Assert.True(accepted);
        Assert.Equal(["recovered", "still-broken"], requests.Select(a => a.DownloadedFileInfo.FileId).Order());
        Assert.All(requests, a => Assert.True(a.DownloadedFileInfo.FromOtherUploads));
        Assert.Equal(["disk:/Photos/recovered.jpg", "disk:/Photos/still-broken.jpg"], handler.DownloadedFiles.Order());

        // the other uploads are not listed on a retry
        _photoService.Verify(a => a.GetSavedExternalIdsAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<IEnumerable<string>>()),
            Times.Never);

        var lastStatus = _reportedStatuses.Last();
        Assert.Equal(PhotoSourceStatus.Done, lastStatus.Status);
        Assert.Equal(10, lastStatus.TotalCount);
        Assert.Equal(9, lastStatus.ProcessedCount);
        Assert.Equal(1, lastStatus.FailedCount);
        Assert.Equal(new RetryProgress(2, 2, 1), retries.Last());

        _failedFileService.Verify(a => a.RecordAsync(UserId, YandexDiskSourceId,
            It.Is<IReadOnlyCollection<FailedFile>>(b => b.Single().ExternalId == "still-broken" && b.Single().FromOtherUploads),
            It.Is<IReadOnlyCollection<string>>(b => b.SequenceEqual(new[] { "recovered" }))));
    }

    [Fact]
    public async Task RetryFailed_ShouldKeepAPhotoFailed_WhenItCannotBeLookedUpAgain()
    {
        // Arrange: the photo is gone from the disk
        var handler = new FakeDiskHandler([], []);

        _failedFileService
            .Setup(a => a.GetOtherUploadsAsync(UserId, YandexDiskSourceId))
            .ReturnsAsync([CreateFailedFile("gone", "disk:/Photos/gone.jpg")]);
        _recordedFailures.Add("gone");
        _userPhotoSourceService
            .Setup(a => a.GetOtherUploadsStatusAsync(UserId, YandexDiskSourceId))
            .ReturnsAsync(new UserPhotoSourceStatus
            {
                UserId = UserId,
                PhotoSourceId = YandexDiskSourceId,
                Status = PhotoSourceStatus.Done,
                TotalCount = 5,
                ProcessedCount = 4,
                FailedCount = 1
            });

        var service = CreateService(handler);

        // Act
        await service.RunOtherUploadsCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.RetryFailed);

        await RunWithWorkerAsync([]);

        // Assert: it was counted as failed by the run it failed in
        var lastStatus = _reportedStatuses.Last();
        Assert.Equal(PhotoSourceStatus.Done, lastStatus.Status);
        Assert.Equal(4, lastStatus.ProcessedCount);
        Assert.Equal(1, lastStatus.FailedCount);

        _failedFileService.Verify(a => a.RecordAsync(UserId, YandexDiskSourceId,
            It.Is<IReadOnlyCollection<FailedFile>>(b => b.Single().ExternalId == "gone" &&
                                                        b.Single().Stage == FileFailureStage.Download),
            It.Is<IReadOnlyCollection<string>>(b => b.Count == 0)));
    }

    [Fact]
    public async Task Run_ShouldCountThePhotosRecordedAsFailed_TheOnesOfEarlierRunsNotReachedYetAmongThem()
    {
        // Arrange: two photos failed in earlier runs, one of them is saved this time and one is gone from the disk
        _recordedFailures.UnionWith(["fixed", "gone"]);

        var handler = new FakeDiskHandler([CreateResource("fixed", "disk:/Photos/fixed.jpg")]);

        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, YandexDiskSourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string>());

        var service = CreateService(handler);

        // Act
        await service.RunOtherUploadsCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Start);

        await RunWithWorkerAsync([]);

        // Assert: the failed count is the table of the failed photos, the one gone stays in it
        Assert.Equal(2, _reportedStatuses.First().FailedCount);
        Assert.Equal(1, _reportedStatuses.Last().FailedCount);
        Assert.Equal(1, _reportedStatuses.Last().ProcessedCount);
    }

    [Fact]
    public async Task Sync_ShouldProcessOnlyThePhotosUploadedBeforeTheLastListingRun_NeitherSavedNorFailed()
    {
        // Arrange
        var lastListedAt = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var before = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var since = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

        var handler = new FakeDiskHandler(
        [
            CreateResource("missed", "disk:/Photos/missed.jpg", created: before),
            CreateResource("saved", "disk:/Photos/saved.jpg", created: before),
            CreateResource("failed", "disk:/Photos/failed.jpg", created: before),
            CreateResource("new", "disk:/Photos/new.jpg", created: since)
        ]);

        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, YandexDiskSourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string> { "saved" });
        _failedFileService
            .Setup(a => a.GetOtherUploadsAsync(UserId, YandexDiskSourceId))
            .ReturnsAsync([CreateFailedFile("failed", "disk:/Photos/failed.jpg")]);
        _recordedFailures.Add("failed");
        _userPhotoSourceService
            .Setup(a => a.GetOtherUploadsStatusAsync(UserId, YandexDiskSourceId))
            .ReturnsAsync(new UserPhotoSourceStatus
            {
                UserId = UserId,
                PhotoSourceId = YandexDiskSourceId,
                Status = PhotoSourceStatus.Done,
                LastUpdatedAt = DateTimeOffset.UtcNow,
                LastListedAt = lastListedAt
            });

        var requests = new List<ProcessImageRequest>();
        var service = CreateService(handler);

        // Act
        var accepted = await service.RunOtherUploadsCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Sync);

        await RunWithWorkerAsync(requests);

        // Assert: the photo uploaded since is left to the next run, the failed one to Retry failed
        Assert.True(accepted);
        Assert.Equal(["missed"], requests.Select(a => a.DownloadedFileInfo.FileId));
        Assert.Equal(["disk:/Photos/missed.jpg"], handler.DownloadedFiles);

        var lastStatus = _reportedStatuses.Last();
        Assert.Equal(PhotoSourceStatus.Done, lastStatus.Status);
        Assert.Equal(2, lastStatus.ProcessedCount);
        Assert.Equal(1, lastStatus.FailedCount);
        Assert.All(_reportedStatuses, a => Assert.Null(a.LastListedAt));
    }

    [Fact]
    public async Task Run_ShouldSaveWhenItStarted_OnceItHasFinished()
    {
        // Arrange
        var handler = new FakeDiskHandler([CreateResource("a", "disk:/Photos/a.jpg")]);

        _photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, YandexDiskSourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string>());

        var service = CreateService(handler);
        var before = DateTimeOffset.UtcNow;

        // Act
        await service.RunOtherUploadsCommandAsync(UserId, YandexDiskSourceId, PhotoSourceProcessingCommands.Start);

        await RunWithWorkerAsync([]);

        // Assert
        var lastStatus = _reportedStatuses.Last();
        Assert.Equal(PhotoSourceStatus.Done, lastStatus.Status);
        Assert.InRange(lastStatus.LastListedAt!.Value, before, DateTimeOffset.UtcNow);
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
        var fileInfo = PhotoSourceProcessingService.CreateFileInfo(resource);

        // Assert
        Assert.Equal(resource.Created, fileInfo.CreatedOn);
        Assert.True(fileInfo.FromOtherUploads);
    }

    /// <summary>
    /// Runs the run started, with a stand-in for the worker and the saving of the photos: each request is reported
    /// processed with the result given, success unless told otherwise. The run waits for every photo it hands over,
    /// so once it has finished, all of them have been.
    /// </summary>
    private Task RunWithWorkerAsync(List<ProcessImageRequest> requests, ProcessingResult? result = null)
    {
        return RunWithWorkerAsync(requests, _ => result ?? ProcessingResult.Success);
    }

    private async Task RunWithWorkerAsync(List<ProcessImageRequest> requests, Func<ProcessImageRequest, ProcessingResult> process)
    {
        using var workerCancellation = new CancellationTokenSource();

        var worker = Task.Run(async () =>
        {
            await foreach (var request in _requestQueue.ReadAllAsync(workerCancellation.Token))
            {
                requests.Add(request);
                Assert.True(_processingRequests.Complete(request.RequestId, process(request)));
            }
        });

        await _startedRun!(CancellationToken.None).WaitAsync(Timeout);

        await workerCancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
    }

    private PhotoSourceProcessingService CreateService(HttpMessageHandler handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(a => a.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));

        var services = new ServiceCollection();
        services.AddSingleton(_userPhotoSourceService.Object);
        services.AddSingleton(_photoService.Object);
        services.AddSingleton(_failedFileService.Object);
        services.AddSingleton(_fileStorage.Object);
        services.AddSingleton(_frontendNotificationService.Object);
        services.AddSingleton<IMessageQueue<ProcessImageRequest>>(_requestQueue);
        services.AddSingleton(_processingRequests);
        services.AddSingleton(httpClientFactory.Object);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return new PhotoSourceProcessingService(_userPhotoSourceService.Object, _photoSourceService.Object,
            _backgroundTaskManager.Object, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new PhotoProcessingSettings { Sizes = [256, 640] }));
    }

    private static object CreateResource(string resourceId, string path, object? exif = null, DateTime? created = null)
    {
        return new
        {
            name = path[(path.LastIndexOf('/') + 1)..],
            path,
            type = "file",
            media_type = "image",
            resource_id = resourceId,
            created = created ?? new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            exif
        };
    }

    private static FailedFile CreateFailedFile(string externalId, string path)
    {
        return new FailedFile
        {
            UserId = UserId,
            PhotoSourceId = YandexDiskSourceId,
            ExternalId = externalId,
            Path = path,
            FileName = path[(path.LastIndexOf('/') + 1)..],
            Stage = FileFailureStage.Processing,
            Error = "Failed to decode the image.",
            Attempts = 1,
            FailedAt = DateTimeOffset.UtcNow,
            FromOtherUploads = true
        };
    }

    private static PhotoSource CreatePhotoSource(long id, string name, Type serviceFactoryType)
    {
        return new PhotoSource
        {
            Id = id,
            Name = name,
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
    /// list of files, and the files themselves. The resources looked up by path are the images, unless others are given.
    /// </summary>
    private sealed class FakeDiskHandler(object[] images, object[]? resources = null) : HttpMessageHandler
    {
        private readonly List<string> _downloadedFiles = [];

        /// <summary>Every download of a file fails, as Yandex.Disk not finding it would.</summary>
        public bool FailDownloads { get; init; }

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

            if (uri.AbsolutePath.EndsWith("/resources/download") && FailDownloads)
            {
                return NotFound();
            }

            if (uri.AbsolutePath.EndsWith("/resources"))
            {
                var path = HttpUtility.ParseQueryString(uri.Query)["path"]!;
                var resource = (resources ?? images)
                    .Select(a => JsonSerializer.SerializeToElement(a))
                    .FirstOrDefault(a => a.GetProperty("path").GetString() == path);

                if (resource.ValueKind == JsonValueKind.Object)
                {
                    return Json(resource);
                }

                if (path != "disk:/")
                {
                    return NotFound();
                }
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

        private static Task<HttpResponseMessage> NotFound()
        {
            var content = new StringContent("{\"error\":\"DiskNotFoundError\"}", Encoding.UTF8, "application/json");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = content });
        }

        private static Task<HttpResponseMessage> Json(object value)
        {
            var content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
