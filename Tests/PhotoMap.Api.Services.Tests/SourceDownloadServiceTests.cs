using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class SourceDownloadServiceTests : IDisposable
{
    private const long UserId = 1;
    private const long DropboxSourceId = 1;
    private const long YandexDiskSourceId = 2;
    private const long OtherSourceId = 3;

    private readonly Mock<IUserPhotoSourceService> _userPhotoSourceService = new();
    private readonly Mock<IPhotoSourceService> _photoSourceService = new();
    private readonly Mock<IBackgroundTaskManager> _backgroundTaskManager = new();
    private readonly Mock<IFileStorage> _fileStorage = new();
    private readonly Mock<IFailedDownloadService> _failedDownloadService = new();
    private readonly Mock<ISourceFilesFactory> _sourceFilesFactory = new();
    private readonly Mock<IFrontendNotificationService> _frontendNotificationService = new();
    private readonly SourceDownloadStatuses _statuses = new();
    private readonly FakeSourceFiles _sourceFiles = new();
    private readonly string _storageDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Every call recording the outcome of downloads: the failed files and the names of the downloaded ones.
    /// </summary>
    private readonly List<(SourceDownloadKind Kind, IReadOnlyCollection<FailedDownload> Failed, IReadOnlyCollection<string> Downloaded,
        IReadOnlyCollection<DownloadedFileKey> DownloadedKeys)> _recorded = [];

    private Func<CancellationToken, Task>? _startedRun;

    public SourceDownloadServiceTests()
    {
        _photoSourceService
            .Setup(a => a.GetByIdAsync(DropboxSourceId))
            .ReturnsAsync(CreatePhotoSource(DropboxSourceId, "Dropbox", typeof(DropboxDownloadServiceFactory)));
        _photoSourceService
            .Setup(a => a.GetByIdAsync(YandexDiskSourceId))
            .ReturnsAsync(CreatePhotoSource(YandexDiskSourceId, "Yandex.Disk", typeof(YandexDiskDownloadServiceFactory)));
        _photoSourceService
            .Setup(a => a.GetByIdAsync(OtherSourceId))
            .ReturnsAsync(CreatePhotoSource(OtherSourceId, "Other", typeof(PhotoSourceDownloadServiceFactory)));
        _userPhotoSourceService
            .Setup(a => a.GetAuthResultAsync(UserId, It.IsAny<long>()))
            .ReturnsAsync(new UserAuthResult { Token = "token", TokenExpiresOn = DateTimeOffset.UtcNow.AddHours(1) });
        _backgroundTaskManager
            .Setup(a => a.TryStartTask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()))
            .Callback<string, Func<CancellationToken, Task>>((_, run) => _startedRun = run)
            .Returns(true);
        _sourceFilesFactory
            .Setup(a => a.Supports(It.Is<PhotoSource>(b => b.Id != OtherSourceId)))
            .Returns(true);
        _sourceFilesFactory
            .Setup(a => a.Create(It.IsAny<PhotoSource>(), It.IsAny<UserAuthResult>()))
            .Returns(_sourceFiles);
        _failedDownloadService
            .Setup(a => a.GetAsync(UserId, It.IsAny<long>(), It.IsAny<SourceDownloadKind>()))
            .ReturnsAsync([]);
        _failedDownloadService
            .Setup(a => a.RecordAsync(UserId, It.IsAny<long>(), It.IsAny<SourceDownloadKind>(),
                It.IsAny<IReadOnlyCollection<FailedDownload>>(), It.IsAny<IReadOnlyCollection<DownloadedFileKey>>()))
            .Callback<long, long, SourceDownloadKind, IReadOnlyCollection<FailedDownload>, IReadOnlyCollection<DownloadedFileKey>>(
                (_, _, kind, failed, downloaded) => _recorded.Add((kind, failed, downloaded.Select(a => a.FileName).ToList(), downloaded)))
            .Returns(Task.CompletedTask);
        _sourceFilesFactory
            .Setup(a => a.SupportsChosenFolders(It.Is<PhotoSource>(b => b.Id == YandexDiskSourceId)))
            .Returns(true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageDirectory))
        {
            Directory.Delete(_storageDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SupportsDownloadsAsync_ShouldSupportYandexDiskAndDropbox()
    {
        var sourceFilesFactory = new SourceFilesFactory(Mock.Of<IHttpClientFactory>(), NullLogger<YandexDiskSourceFiles>.Instance);

        Assert.True(sourceFilesFactory.Supports((await _photoSourceService.Object.GetByIdAsync(YandexDiskSourceId))));
        Assert.True(sourceFilesFactory.Supports((await _photoSourceService.Object.GetByIdAsync(DropboxSourceId))));
        Assert.False(sourceFilesFactory.Supports((await _photoSourceService.Object.GetByIdAsync(OtherSourceId))));
    }

    [Fact]
    public async Task StartAsync_ShouldDownloadOnlyTheMissingFiles_AndRecordTheOnesThatFail()
    {
        // Arrange
        _sourceFiles.Add("downloaded.jpg", [1, 2, 3]);
        _sourceFiles.Add("partial.jpg", [1, 2, 3, 4]);
        _sourceFiles.Add("missing.mp4", [5, 6], SourceDownloadContent.Videos);
        _sourceFiles.Add("broken.jpg", [7], fails: true);

        await StoreAsync("downloaded.jpg", [1, 2, 3]);
        await StoreAsync("partial.jpg", [1, 2]);

        var service = CreateService();

        // Act
        await service.StartAsync(UserId, YandexDiskSourceId, SourceDownloadContent.All);
        await _startedRun!(CancellationToken.None);

        // Assert
        Assert.Equal(SourceDownloadContent.All, _sourceFiles.ListedContent);
        Assert.Equal(["broken.jpg", "missing.mp4", "partial.jpg"], _sourceFiles.Downloaded.Order());
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(StoredPath("partial.jpg")));
        Assert.Equal([5, 6], await File.ReadAllBytesAsync(StoredPath("missing.mp4")));

        // the file found whole is no longer failed, the rest are recorded as they went
        Assert.Equal(["downloaded.jpg"], _recorded[0].Downloaded);
        Assert.Equal(["missing.mp4", "partial.jpg"], _recorded.Skip(1).SelectMany(a => a.Downloaded).Order());

        var failed = Assert.Single(_recorded.SelectMany(a => a.Failed));
        Assert.Equal("broken.jpg", failed.FileName);
        Assert.Equal("disk:/Camera Uploads/broken.jpg", failed.Path);
        Assert.Equal("The connection was reset.", failed.Error);

        var status = service.GetStatus(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder)!;
        Assert.Equal(PhotoSourceStatus.Done, status.Status);
        Assert.False(status.RetryingFailed);
        Assert.Equal(4, status.TotalCount);
        Assert.Equal(1, status.AlreadyDownloadedCount);
        Assert.Equal(3, status.DownloadedCount);
        Assert.Equal(1, status.FailedCount);
    }

    [Fact]
    public async Task StartAsync_ShouldKeepDownloadingTheOtherFiles_WhileALargeFileIsDownloading()
    {
        // Arrange: the first file only finishes once all the others have, more of them than are recorded at a time
        var otherFileNames = Enumerable.Range(1, 80).Select(a => $"photo_{a:000}.jpg").ToList();
        _sourceFiles.Add("large.mp4", [1], SourceDownloadContent.Videos);
        otherFileNames.ForEach(a => _sourceFiles.Add(a, [2]));
        _sourceFiles.BeforeDownload = async (fileName, token) =>
        {
            while (fileName == "large.mp4" && _sourceFiles.Downloaded.Count() < otherFileNames.Count)
            {
                await Task.Delay(5, token);
            }
        };

        var service = CreateService();

        // Act
        await service.StartAsync(UserId, YandexDiskSourceId, SourceDownloadContent.All);
        await _startedRun!(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal("large.mp4", _sourceFiles.Downloaded.Last());

        var status = service.GetStatus(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder)!;
        Assert.Equal(PhotoSourceStatus.Done, status.Status);
        Assert.Equal(81, status.DownloadedCount);
        Assert.Equal(81, _recorded.Skip(1).SelectMany(a => a.Downloaded).Count());
    }

    [Fact]
    public async Task StartAsync_ShouldRecordTheFilesFinished_WhenTheDownloadIsStopped()
    {
        // Arrange: one file never finishes, the download is stopped once the others have
        using var cancellationTokenSource = new CancellationTokenSource();

        _sourceFiles.Add("stuck.mp4", [1], SourceDownloadContent.Videos);
        _sourceFiles.Add("photo_1.jpg", [2]);
        _sourceFiles.Add("photo_2.jpg", [3]);
        _sourceFiles.BeforeDownload = async (fileName, token) =>
        {
            if (fileName == "stuck.mp4")
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            else if (_sourceFiles.Downloaded.Count() == 1)
            {
                await cancellationTokenSource.CancelAsync();
            }
        };

        var service = CreateService();

        // Act
        await service.StartAsync(UserId, YandexDiskSourceId, SourceDownloadContent.All);
        await _startedRun!(cancellationTokenSource.Token).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(PhotoSourceStatus.Stopped, service.GetStatus(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder)!.Status);
        Assert.DoesNotContain("stuck.mp4", _recorded.SelectMany(a => a.Downloaded));
        Assert.Empty(_recorded.SelectMany(a => a.Failed));
        Assert.Contains("photo_1.jpg", _recorded.Skip(1).SelectMany(a => a.Downloaded));
    }

    [Fact]
    public async Task StartAsync_ShouldFailTheDownload_WhenTheOutcomesCannotBeRecorded()
    {
        // Arrange: the files found downloaded are recorded, the first file downloaded is not
        _sourceFiles.Add("photo_1.jpg", [1]);
        _sourceFiles.Add("photo_2.jpg", [2]);
        _failedDownloadService
            .Setup(a => a.RecordAsync(UserId, YandexDiskSourceId, It.IsAny<SourceDownloadKind>(),
                It.IsAny<IReadOnlyCollection<FailedDownload>>(), It.Is<IReadOnlyCollection<DownloadedFileKey>>(b => b.Count > 0)))
            .ThrowsAsync(new InvalidOperationException("The database is unavailable."));

        var service = CreateService();

        // Act
        await service.StartAsync(UserId, YandexDiskSourceId, SourceDownloadContent.All);
        await _startedRun!(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(PhotoSourceStatus.Failed, service.GetStatus(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder)!.Status);
        _frontendNotificationService.Verify(a => a.SendDownloadErrorAsync(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder,
            "The database is unavailable."));
    }

    [Fact]
    public async Task StartFolderAsync_ShouldDownloadTheFolderChosen_IntoTheFoldersOfItsPath()
    {
        // Arrange: a file of the same name in the folder the photos are processed from is left alone
        _sourceFiles.Add("IMG_0001.jpg", [1]);
        _sourceFiles.Add("IMG_0001.jpg", [2, 2], folder: "disk:/Photos/2020");
        _sourceFiles.Add("trip.mp4", [3, 3, 3], SourceDownloadContent.Videos, folder: "disk:/Photos/2020");
        _sourceFiles.Add("broken.jpg", [4], fails: true, folder: "disk:/Photos/2020");

        var service = CreateService();

        // Act
        var accepted = await service.StartFolderAsync(UserId, YandexDiskSourceId, " /Photos/2020/ ", SourceDownloadContent.All);
        await _startedRun!(CancellationToken.None);

        // Assert
        Assert.True(accepted);
        _backgroundTaskManager.Verify(a => a.TryStartTask("FolderDownload-UserId=1-SourceId=2", It.IsAny<Func<CancellationToken, Task>>()));

        Assert.Equal("disk:/Photos/2020", _sourceFiles.ListedFolder);
        Assert.Equal([2, 2], await File.ReadAllBytesAsync(FolderStoredPath("Photos", "2020", "IMG_0001.jpg")));
        Assert.Equal([3, 3, 3], await File.ReadAllBytesAsync(FolderStoredPath("Photos", "2020", "trip.mp4")));
        Assert.False(File.Exists(StoredPath("IMG_0001.jpg")));

        Assert.All(_recorded, a => Assert.Equal(SourceDownloadKind.ChosenFolder, a.Kind));
        Assert.All(_recorded.SelectMany(a => a.DownloadedKeys), a => Assert.Equal("disk:/Photos/2020", a.Folder));

        var failed = Assert.Single(_recorded.SelectMany(a => a.Failed));
        Assert.Equal(SourceDownloadKind.ChosenFolder, failed.Kind);
        Assert.Equal("disk:/Photos/2020", failed.Folder);
        Assert.Equal("disk:/Photos/2020/broken.jpg", failed.Path);

        var status = service.GetStatus(UserId, YandexDiskSourceId, SourceDownloadKind.ChosenFolder)!;
        Assert.Equal("disk:/Photos/2020", status.Folder);
        Assert.Equal(3, status.TotalCount);
        Assert.Equal(2, status.DownloadedCount);
        Assert.Equal(1, status.FailedCount);
        Assert.Null(service.GetStatus(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder));
        _frontendNotificationService.Verify(a => a.SendDownloadProgressAsync(It.IsAny<UserPhotoSourceStatus>(),
            SourceDownloadKind.ChosenFolder));
        _frontendNotificationService.Verify(a => a.SendDownloadProgressAsync(It.IsAny<UserPhotoSourceStatus>(),
            SourceDownloadKind.ProcessingFolder), Times.Never);
    }

    [Fact]
    public async Task StartFolderAsync_ShouldSkipTheFilesOfTheFolderDownloadedAlready()
    {
        // Arrange
        _sourceFiles.Add("IMG_0001.jpg", [1, 1], folder: "disk:/Photos");
        _sourceFiles.Add("IMG_0002.jpg", [2, 2], folder: "disk:/Photos");

        await new FileStorage(new FileStorageSettings { BasePath = _storageDirectory })
            .SaveAsync(Path.Combine("Yandex.Disk", "1", "folders", "Photos", "IMG_0001.jpg"), [1, 1]);

        var service = CreateService();

        // Act
        await service.StartFolderAsync(UserId, YandexDiskSourceId, "disk:/Photos", SourceDownloadContent.Photos);
        await _startedRun!(CancellationToken.None);

        // Assert
        Assert.Equal(["IMG_0002.jpg"], _sourceFiles.Downloaded);
        Assert.Equal([new DownloadedFileKey("disk:/Photos", "IMG_0001.jpg")], _recorded[0].DownloadedKeys);
        Assert.Equal(1, service.GetStatus(UserId, YandexDiskSourceId, SourceDownloadKind.ChosenFolder)!.AlreadyDownloadedCount);
    }

    [Fact]
    public async Task RetryFailedAsync_ShouldDownloadTheFailedFilesOfTheChosenFolders_IntoTheirOwnFolders()
    {
        // Arrange: the files that failed in two folders, of the same name
        _failedDownloadService
            .Setup(a => a.GetAsync(UserId, YandexDiskSourceId, SourceDownloadKind.ChosenFolder))
            .ReturnsAsync([
                CreateFailedDownload("IMG_0001.jpg", "disk:/Photos/2020"),
                CreateFailedDownload("IMG_0001.jpg", "disk:/Photos/2021")
            ]);

        _sourceFiles.Add("IMG_0001.jpg", [20], folder: "disk:/Photos/2020");
        _sourceFiles.Add("IMG_0001.jpg", [21], folder: "disk:/Photos/2021");

        var service = CreateService();

        // Act
        await service.RetryFailedAsync(UserId, YandexDiskSourceId, SourceDownloadKind.ChosenFolder);
        await _startedRun!(CancellationToken.None);

        // Assert
        Assert.Null(_sourceFiles.ListedContent);
        Assert.Equal([20], await File.ReadAllBytesAsync(FolderStoredPath("Photos", "2020", "IMG_0001.jpg")));
        Assert.Equal([21], await File.ReadAllBytesAsync(FolderStoredPath("Photos", "2021", "IMG_0001.jpg")));
        Assert.Equal(
            [new DownloadedFileKey("disk:/Photos/2020", "IMG_0001.jpg"), new DownloadedFileKey("disk:/Photos/2021", "IMG_0001.jpg")],
            _recorded.Skip(1).SelectMany(a => a.DownloadedKeys).OrderBy(a => a.Folder));
        Assert.True(service.GetStatus(UserId, YandexDiskSourceId, SourceDownloadKind.ChosenFolder)!.RetryingFailed);
    }

    [Fact]
    public async Task StartFolderAsync_ShouldNotStartTheDownload_OfASourceWhoseFoldersCannotBeChosen()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartFolderAsync(UserId, DropboxSourceId, "/Photos", SourceDownloadContent.All));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("disk:/Photos/../../etc")]
    [InlineData("Photos/./2020")]
    [InlineData("Photos\\..\\2020")]
    public async Task StartFolderAsync_ShouldNotStartTheDownload_OfAFolderThatCannotBeDownloadedFrom(string folder)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.StartFolderAsync(UserId, YandexDiskSourceId, folder, SourceDownloadContent.All));

        _backgroundTaskManager.Verify(a => a.TryStartTask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()),
            Times.Never);
    }

    [Fact]
    public async Task StartFolderAsync_ShouldRunAlongsideTheDownloadOfTheProcessingFolder()
    {
        // Arrange
        _backgroundTaskManager.Setup(a => a.IsRunning("Download-UserId=1-SourceId=2")).Returns(true);

        var service = CreateService();

        // Act
        var accepted = await service.StartFolderAsync(UserId, YandexDiskSourceId, "/Photos", SourceDownloadContent.All);

        // Assert
        Assert.True(accepted);
    }

    [Theory]
    [InlineData("Photos/2020", "disk:/Photos/2020")]
    [InlineData("/Photos/2020/", "disk:/Photos/2020")]
    [InlineData("disk:/Photos/2020", "disk:/Photos/2020")]
    [InlineData("DISK:/Photos//2020", "disk:/Photos/2020")]
    [InlineData("/", "disk:/")]
    [InlineData("disk:/", "disk:/")]
    [InlineData("", null)]
    [InlineData("/Photos/..", null)]
    [InlineData("/Photos/.", null)]
    public void NormalizeFolder_ShouldTakeTheFolderAsYandexDiskDoes(string folder, string? expectedFolder)
    {
        Assert.Equal(expectedFolder, YandexDiskPaths.NormalizeFolder(folder));
    }

    [Theory]
    [InlineData("disk:/Photos/2020/IMG_0001.jpg", "disk:/Photos/2020")]
    [InlineData("disk:/IMG_0001.jpg", "disk:/")]
    public void GetFolder_ShouldLeaveTheFileNameOut(string path, string expectedFolder)
    {
        Assert.Equal(expectedFolder, YandexDiskPaths.GetFolder(path));
    }

    [Theory]
    [InlineData("disk:/../IMG_0001.jpg")]
    [InlineData("disk:/Photos/../../IMG_0001.jpg")]
    [InlineData("disk:/")]
    public void ToStoragePath_ShouldNotLeaveTheFolderGiven(string path)
    {
        Assert.Throws<ArgumentException>(() => YandexDiskPaths.ToStoragePath("folders", path));
    }

    [Fact]
    public async Task StartAsync_ShouldListTheContentChosen()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.StartAsync(UserId, DropboxSourceId, SourceDownloadContent.Videos);
        await _startedRun!(CancellationToken.None);

        // Assert
        Assert.Equal(SourceDownloadContent.Videos, _sourceFiles.ListedContent);
    }

    [Fact]
    public async Task RetryFailedAsync_ShouldDownloadOnlyTheFailedFiles_WithoutListingTheFolder()
    {
        // Arrange
        _failedDownloadService
            .Setup(a => a.GetAsync(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder))
            .ReturnsAsync([CreateFailedDownload("broken.jpg"), CreateFailedDownload("fixed.jpg")]);

        _sourceFiles.Add("broken.jpg", [1, 2]);
        _sourceFiles.Add("fixed.jpg", [3]);
        _sourceFiles.Add("other.jpg", [4]);

        await StoreAsync("fixed.jpg", [3]);

        var service = CreateService();

        // Act
        var accepted = await service.RetryFailedAsync(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder);
        await _startedRun!(CancellationToken.None);

        // Assert
        Assert.True(accepted);
        Assert.Null(_sourceFiles.ListedContent);
        Assert.Equal(["broken.jpg"], _sourceFiles.Downloaded);

        Assert.Equal(["fixed.jpg"], _recorded[0].Downloaded);
        Assert.Equal(["broken.jpg"], _recorded.Skip(1).SelectMany(a => a.Downloaded));
        Assert.Empty(_recorded.SelectMany(a => a.Failed));

        var status = service.GetStatus(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder)!;
        Assert.True(status.RetryingFailed);
        Assert.Equal(2, status.TotalCount);
        Assert.Equal(2, status.DownloadedCount);
        Assert.Equal(0, status.FailedCount);
    }

    [Fact]
    public async Task RetryFailedAsync_ShouldNotStartTheRetry_WhileTheDownloadIsRunning()
    {
        // Arrange
        _backgroundTaskManager.Setup(a => a.IsRunning("Download-UserId=1-SourceId=2")).Returns(true);

        var service = CreateService();

        // Act
        var accepted = await service.RetryFailedAsync(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder);

        // Assert
        Assert.False(accepted);
    }

    [Fact]
    public async Task StartAsync_ShouldFailTheDownload_WhenTheSourceNoLongerAcceptsTheToken()
    {
        // Arrange
        _sourceFiles.Add("photo.jpg", [1]);
        _sourceFiles.AuthErrorOn = "photo.jpg";

        var service = CreateService();

        // Act
        await service.StartAsync(UserId, YandexDiskSourceId, SourceDownloadContent.Photos);
        await _startedRun!(CancellationToken.None);

        // Assert
        Assert.Equal(PhotoSourceStatus.Failed, service.GetStatus(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder)!.Status);
        _frontendNotificationService.Verify(a => a.SendDownloadErrorAsync(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder,
            It.IsAny<string>()));
    }

    [Theory]
    [InlineData(SourceDownloadContent.Photos)]
    [InlineData(SourceDownloadContent.Videos)]
    [InlineData(SourceDownloadContent.All)]
    public async Task StartAsync_ShouldStartTheDownloadInTheBackground(SourceDownloadContent content)
    {
        // Arrange
        var service = CreateService();

        // Act
        var accepted = await service.StartAsync(UserId, YandexDiskSourceId, content);

        // Assert
        Assert.True(accepted);

        _backgroundTaskManager.Verify(a => a.TryStartTask("Download-UserId=1-SourceId=2", It.IsAny<Func<CancellationToken, Task>>()));
    }

    [Fact]
    public async Task StartAsync_ShouldNotStartTheDownload_WhileItIsRunning()
    {
        // Arrange
        _backgroundTaskManager.Setup(a => a.IsRunning("Download-UserId=1-SourceId=2")).Returns(true);

        var service = CreateService();

        // Act
        var accepted = await service.StartAsync(UserId, YandexDiskSourceId, SourceDownloadContent.All);

        // Assert
        Assert.False(accepted);

        _backgroundTaskManager.Verify(a => a.TryStartTask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()),
            Times.Never);
    }

    [Fact]
    public async Task StartAsync_ShouldNotStartTheDownload_WhenTheUserIsNotAuthorized()
    {
        // Arrange
        _userPhotoSourceService
            .Setup(a => a.GetAuthResultAsync(UserId, DropboxSourceId))
            .ReturnsAsync(new UserAuthResult { Token = "token", TokenExpiresOn = DateTimeOffset.UtcNow.AddHours(-1) });

        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<NotAuthorizedException>(() =>
            service.StartAsync(UserId, DropboxSourceId, SourceDownloadContent.Photos));
    }

    [Fact]
    public async Task StartAsync_ShouldNotStartTheDownload_OfASourceItCannotDownload()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartAsync(UserId, OtherSourceId, SourceDownloadContent.Photos));
    }

    [Fact]
    public async Task StartAsync_ShouldNotStartTheDownload_OfNeitherPhotosNorVideos()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.StartAsync(UserId, YandexDiskSourceId, (SourceDownloadContent)0));
    }

    [Fact]
    public async Task GetFailedDownloadsAsync_ShouldBeTheFailedFilesOfTheSource()
    {
        // Arrange
        _failedDownloadService
            .Setup(a => a.GetAsync(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder))
            .ReturnsAsync([CreateFailedDownload("broken.jpg")]);

        var service = CreateService();

        // Act
        var failedDownloads = await service.GetFailedDownloadsAsync(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder);

        // Assert
        Assert.Equal("broken.jpg", Assert.Single(failedDownloads).FileName);
    }

    [Fact]
    public void Stop_ShouldCancelTheDownload()
    {
        var service = CreateService();

        service.Stop(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder);
        service.Stop(UserId, YandexDiskSourceId, SourceDownloadKind.ChosenFolder);

        _backgroundTaskManager.Verify(a => a.CancelTask("Download-UserId=1-SourceId=2"));
        _backgroundTaskManager.Verify(a => a.CancelTask("FolderDownload-UserId=1-SourceId=2"));
    }

    [Fact]
    public void GetStatus_ShouldBeTheLastStatusOfTheSource()
    {
        // Arrange
        var status = new SourceDownloadStatus(PhotoSourceStatus.Stopped, SourceDownloadContent.Videos, null, false, 10, 4, 2, 1,
            DateTimeOffset.UtcNow);
        _statuses.Set(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder, status);

        var service = CreateService();

        // Act & Assert
        Assert.Equal(status, service.GetStatus(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder));
        Assert.Null(service.GetStatus(UserId, DropboxSourceId, SourceDownloadKind.ProcessingFolder));
        Assert.Null(service.GetStatus(UserId, YandexDiskSourceId, SourceDownloadKind.ChosenFolder));
    }

    [Fact]
    public async Task GetTargetFolderAsync_ShouldBeTheDownloadsFolderOfTheSourceInTheFileStorage()
    {
        // Arrange
        _fileStorage.Setup(a => a.GetFullPath(It.IsAny<string>())).Returns<string>(a => "/storage/" + a);

        var service = CreateService();

        // Act
        var targetFolder = await service.GetTargetFolderAsync(UserId, YandexDiskSourceId, SourceDownloadKind.ProcessingFolder);
        var chosenFolderTargetFolder = await service.GetTargetFolderAsync(UserId, YandexDiskSourceId, SourceDownloadKind.ChosenFolder);

        // Assert
        Assert.Equal("/storage/" + Path.Combine("Yandex.Disk", "1", "downloads"), targetFolder);
        Assert.Equal("/storage/" + Path.Combine("Yandex.Disk", "1", "folders"), chosenFolderTargetFolder);
    }

    [Theory]
    [InlineData("IMG_0001.JPG", null, SourceDownloadContent.Photos)]
    [InlineData("IMG_0001.heic", null, SourceDownloadContent.Photos)]
    [InlineData("IMG_0001.DNG", null, SourceDownloadContent.Photos)]
    [InlineData("VID_0001.mp4", null, SourceDownloadContent.Videos)]
    [InlineData("VID_0001.MOV", null, SourceDownloadContent.Videos)]
    [InlineData("scan.raw", "image", SourceDownloadContent.Photos)]
    [InlineData("clip.dat", "video", SourceDownloadContent.Videos)]
    [InlineData("notes.txt", null, null)]
    [InlineData("notes.txt", "document", null)]
    public void GetContent_ShouldTellPhotosFromVideos(string fileName, string? mediaType, SourceDownloadContent? expectedContent)
    {
        Assert.Equal(expectedContent, DownloadableFormats.GetContent(fileName, mediaType));
    }

    [Fact]
    public async Task FileStorage_SaveAsync_ShouldLeaveNoFileBehind_WhenTheContentsFailPartway()
    {
        // Arrange
        var baseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var fileStorage = new FileStorage(new FileStorageSettings { BasePath = baseDirectory });

        try
        {
            // Act
            await Assert.ThrowsAsync<IOException>(() => fileStorage.SaveAsync("downloads/video.mp4", async (stream, token) =>
            {
                await stream.WriteAsync(new byte[] { 1, 2, 3 }, token);
                throw new IOException("The connection was reset.");
            }, CancellationToken.None));

            await fileStorage.SaveAsync("downloads/photo.jpg",
                (stream, token) => stream.WriteAsync(new byte[] { 1, 2, 3, 4 }, token).AsTask(), CancellationToken.None);

            // Assert
            Assert.Null(fileStorage.GetSize("downloads/video.mp4"));
            Assert.Equal(4, fileStorage.GetSize("downloads/photo.jpg"));
            Assert.Equal(["photo.jpg"], Directory.GetFiles(Path.Combine(baseDirectory, "downloads")).Select(Path.GetFileName));
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    /// <summary>
    /// The run resolves its services from a scope of its own, these are the ones it gets.
    /// </summary>
    private SourceDownloadService CreateService()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IFileStorage>(new FileStorage(new FileStorageSettings { BasePath = _storageDirectory }))
            .AddSingleton(_failedDownloadService.Object)
            .AddSingleton(_frontendNotificationService.Object)
            .AddSingleton(_sourceFilesFactory.Object)
            .AddSingleton(_statuses)
            .BuildServiceProvider();

        return new SourceDownloadService(_userPhotoSourceService.Object, _photoSourceService.Object, _backgroundTaskManager.Object,
            services.GetRequiredService<IServiceScopeFactory>(), _fileStorage.Object, _failedDownloadService.Object,
            _sourceFilesFactory.Object, _statuses);
    }

    private static string RelativePath(string fileName) => Path.Combine("Yandex.Disk", "1", "downloads", fileName);

    private string StoredPath(string fileName) => Path.Combine(_storageDirectory, RelativePath(fileName));

    private Task StoreAsync(string fileName, byte[] contents)
    {
        return new FileStorage(new FileStorageSettings { BasePath = _storageDirectory }).SaveAsync(RelativePath(fileName), contents);
    }

    private string FolderStoredPath(params string[] segments) =>
        Path.Combine([_storageDirectory, "Yandex.Disk", "1", "folders", ..segments]);

    /// <param name="folder">The chosen folder the file is in, the folder the photos are processed from when not given.</param>
    private static FailedDownload CreateFailedDownload(string fileName, string? folder = null)
    {
        return new FailedDownload
        {
            UserId = UserId,
            PhotoSourceId = YandexDiskSourceId,
            Kind = folder == null ? SourceDownloadKind.ProcessingFolder : SourceDownloadKind.ChosenFolder,
            Folder = folder ?? "",
            FileName = fileName,
            Path = $"{folder ?? FakeSourceFiles.ProcessingFolder}/{fileName}",
            Size = fileName == "broken.jpg" ? 2 : 1,
            Content = SourceDownloadContent.Photos,
            Error = "The connection was reset.",
            Attempts = 1,
            FailedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// The files of a folder, downloaded from memory, some of them failing.
    /// </summary>
    private sealed class FakeSourceFiles : ISourceFiles
    {
        public const string ProcessingFolder = "disk:/Camera Uploads";

        private readonly List<SourceFile> _files = [];
        private readonly Dictionary<string, byte[]> _contents = [];
        private readonly HashSet<string> _failing = [];
        private readonly ConcurrentQueue<string> _downloaded = new();

        public int MaxParallelDownloads => 2;
        public SourceDownloadContent? ListedContent { get; private set; }
        public string? ListedFolder { get; private set; }
        public IEnumerable<string> Downloaded => _downloaded;
        public string? AuthErrorOn { get; set; }

        /// <summary>
        /// Called before a file is downloaded, to hold it up.
        /// </summary>
        public Func<string, CancellationToken, Task>? BeforeDownload { get; set; }

        /// <param name="folder">The folder of the file, the one the photos are processed from when not given.</param>
        public void Add(string name, byte[] contents, SourceDownloadContent content = SourceDownloadContent.Photos, bool fails = false,
            string folder = ProcessingFolder)
        {
            var path = $"{folder}/{name}";

            _files.Add(new SourceFile(name, path, contents.Length, content));
            _contents[path] = contents;

            if (fails)
            {
                _failing.Add(path);
            }
        }

        public Task<IReadOnlyList<SourceFile>> ListAsync(SourceDownloadContent content, string? folder,
            CancellationToken cancellationToken)
        {
            ListedContent = content;
            ListedFolder = folder;

            var files = _files.Where(a => content.HasFlag(a.Content) && YandexDiskPaths.GetFolder(a.Path) == (folder ?? ProcessingFolder));

            return Task.FromResult<IReadOnlyList<SourceFile>>(files.ToList());
        }

        public async Task CopyToAsync(SourceFile file, Stream destination, CancellationToken cancellationToken)
        {
            if (BeforeDownload != null)
            {
                await BeforeDownload(file.Name, cancellationToken);
            }

            _downloaded.Enqueue(file.Name);

            if (file.Name == AuthErrorOn)
            {
                throw new YandexDiskException("The token has expired.", isAuthError: true);
            }

            await destination.WriteAsync(_contents[file.Path], cancellationToken);

            if (_failing.Contains(file.Path))
            {
                throw new IOException("The connection was reset.");
            }
        }

        public Task<SourcePreview?> DownloadPreviewAsync(string reference, CancellationToken cancellationToken)
        {
            return Task.FromResult<SourcePreview?>(null);
        }

        public bool IsAuthError(Exception exception)
        {
            return exception is PhotoSourceException { IsAuthError: true };
        }

        public void Dispose()
        {
        }
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
