using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Yandex.Disk;

namespace PhotoMap.Api.Services.Tests;

public class FailedPreviewsDownloaderTests
{
    private const long UserId = 1;
    private const long SourceId = 2;

    private readonly Mock<IFailedPreviewService> _failedPreviewService = new();
    private readonly Mock<IPhotoSourceService> _photoSourceService = new();
    private readonly Mock<IUserPhotoSourceService> _userPhotoSourceService = new();
    private readonly Mock<ISourceFilesFactory> _sourceFilesFactory = new();
    private readonly Mock<IFileStorage> _fileStorage = new();
    private readonly Mock<ISourceFiles> _sourceFiles = new();

    private readonly List<(FailedPreviewTable Table, long Id, string Error, bool CountAttempt)> _failed = [];
    private readonly List<(FailedPreviewTable Table, long Id, string Path, string ContentType)> _saved = [];

    public FailedPreviewsDownloaderTests()
    {
        var photoSource = new PhotoSource
        {
            Id = SourceId,
            Name = "Yandex.Disk",
            ServiceSettings = "{}",
            ClientAuthSettings = null!,
            ServiceFactoryImplementationType = TypeHelper.GetTypeFullName(typeof(YandexDiskDownloadServiceFactory))
        };

        _photoSourceService.Setup(a => a.GetByIdAsync(SourceId)).ReturnsAsync(photoSource);
        _userPhotoSourceService
            .Setup(a => a.GetAuthResultAsync(UserId, SourceId))
            .ReturnsAsync(new UserAuthResult { Token = "token", TokenExpiresOn = DateTimeOffset.UtcNow.AddHours(1) });
        _sourceFilesFactory.Setup(a => a.Supports(photoSource)).Returns(true);
        _sourceFilesFactory.Setup(a => a.Create(photoSource, It.IsAny<UserAuthResult>())).Returns(_sourceFiles.Object);
        _sourceFiles.Setup(a => a.IsAuthError(It.IsAny<Exception>())).Returns<Exception>(e => e is PhotoSourceException { IsAuthError: true });
        _failedPreviewService
            .Setup(a => a.PreviewFailedAsync(It.IsAny<FailedPreviewTable>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<FailedPreviewTable, long, string, bool>((table, id, error, countAttempt) => _failed.Add((table, id, error, countAttempt)))
            .Returns(Task.CompletedTask);
        _failedPreviewService
            .Setup(a => a.SavePreviewAsync(It.IsAny<FailedPreviewTable>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<FailedPreviewTable, long, string, string>((table, id, path, contentType) => _saved.Add((table, id, path, contentType)))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task DownloadPendingAsync_ShouldSaveThePreviewsOfTheFailedFilesAndDownloads()
    {
        // Arrange
        Pending(CreateFailedPreview(FailedPreviewTable.FailedFiles, 7, "disk:/Camera Uploads/IMG_0001.jpg"),
            CreateFailedPreview(FailedPreviewTable.FailedDownloads, 8, "disk:/Photos/IMG_0002.heic"));
        _sourceFiles
            .Setup(a => a.DownloadPreviewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SourcePreview([1, 2, 3], "image/jpeg"));

        // Act
        var downloadedCount = await CreateDownloader().DownloadPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, downloadedCount);

        var filePreviewPath = Path.Combine("Yandex.Disk", "1", "failed-previews", "file-7.jpg");
        var downloadPreviewPath = Path.Combine("Yandex.Disk", "1", "failed-previews", "download-8.jpg");

        _sourceFiles.Verify(a => a.DownloadPreviewAsync("disk:/Camera Uploads/IMG_0001.jpg", It.IsAny<CancellationToken>()));
        _sourceFiles.Verify(a => a.DownloadPreviewAsync("disk:/Photos/IMG_0002.heic", It.IsAny<CancellationToken>()));
        _fileStorage.Verify(a => a.SaveAsync(filePreviewPath, new byte[] { 1, 2, 3 }));
        _fileStorage.Verify(a => a.SaveAsync(downloadPreviewPath, new byte[] { 1, 2, 3 }));
        Assert.Contains((FailedPreviewTable.FailedFiles, 7L, filePreviewPath, "image/jpeg"), _saved);
        Assert.Contains((FailedPreviewTable.FailedDownloads, 8L, downloadPreviewPath, "image/jpeg"), _saved);
        Assert.Empty(_failed);
    }

    [Fact]
    public async Task DownloadPendingAsync_ShouldCountTheAttempt_WhenTheSourceHasNoPreviewOrItFails()
    {
        // Arrange
        Pending(CreateFailedPreview(FailedPreviewTable.FailedFiles, 1, "disk:/video.mov"),
            CreateFailedPreview(FailedPreviewTable.FailedFiles, 2, "disk:/broken.jpg"));
        _sourceFiles
            .Setup(a => a.DownloadPreviewAsync("disk:/video.mov", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SourcePreview?)null);
        _sourceFiles
            .Setup(a => a.DownloadPreviewAsync("disk:/broken.jpg", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new YandexDiskException("The resource has not been found."));

        // Act
        var downloadedCount = await CreateDownloader().DownloadPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, downloadedCount);
        Assert.Contains((FailedPreviewTable.FailedFiles, 1L, "Yandex.Disk has no preview of the file.", true), _failed);
        Assert.Contains((FailedPreviewTable.FailedFiles, 2L, "The resource has not been found.", true), _failed);
        _fileStorage.Verify(a => a.SaveAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task DownloadPendingAsync_ShouldNotCountTheAttempt_WhenTheTokenIsNotAccepted()
    {
        // Arrange
        Pending(CreateFailedPreview(FailedPreviewTable.FailedDownloads, 3, "disk:/IMG_0001.jpg"));
        _sourceFiles
            .Setup(a => a.DownloadPreviewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new YandexDiskException("Unauthorized", isAuthError: true));

        // Act
        await CreateDownloader().DownloadPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal([(FailedPreviewTable.FailedDownloads, 3L, "Yandex.Disk no longer accepts the access token.", false)], _failed);
    }

    [Fact]
    public async Task DownloadPendingAsync_ShouldPutThePreviewsOff_WhileTheSourceIsNotAuthorized()
    {
        // Arrange
        Pending(CreateFailedPreview(FailedPreviewTable.FailedFiles, 4, "disk:/IMG_0001.jpg"));
        _userPhotoSourceService
            .Setup(a => a.GetAuthResultAsync(UserId, SourceId))
            .ReturnsAsync(new UserAuthResult { Token = "token", TokenExpiresOn = DateTimeOffset.UtcNow.AddHours(-1) });

        // Act
        await CreateDownloader().DownloadPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal([(FailedPreviewTable.FailedFiles, 4L, "Yandex.Disk is not authorized.", false)], _failed);
        _sourceFilesFactory.Verify(a => a.Create(It.IsAny<PhotoSource>(), It.IsAny<UserAuthResult>()), Times.Never);
    }

    [Fact]
    public async Task DownloadPendingAsync_ShouldDeleteThePreview_WhenTheFileNoLongerFails()
    {
        // Arrange
        Pending(CreateFailedPreview(FailedPreviewTable.FailedDownloads, 5, "disk:/IMG_0001.jpg"));
        _sourceFiles
            .Setup(a => a.DownloadPreviewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SourcePreview([1], "image/png"));
        _failedPreviewService
            .Setup(a => a.SavePreviewAsync(It.IsAny<FailedPreviewTable>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var downloadedCount = await CreateDownloader().DownloadPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, downloadedCount);
        _fileStorage.Verify(a => a.Delete(Path.Combine("Yandex.Disk", "1", "failed-previews", "download-5.png")));
    }

    [Fact]
    public async Task DownloadPendingAsync_ShouldTakeOnlyThePreviewsNotTriedLately()
    {
        // Arrange
        Pending();

        // Act
        await CreateDownloader().DownloadPendingAsync(CancellationToken.None);

        // Assert
        _failedPreviewService.Verify(a => a.GetPendingAsync(FailedPreviewsDownloader.MaxPreviewAttempts,
            It.Is<DateTimeOffset>(b => b < DateTimeOffset.UtcNow - FailedPreviewsDownloader.RetryAfter + TimeSpan.FromMinutes(1)),
            It.IsAny<int>()));
    }

    [Fact]
    public async Task YandexDiskSourceFiles_DownloadPreviewAsync_ShouldTakeTheXxxlPreview()
    {
        // Arrange
        var handler = new FakePreviewHandler(["XL", "XXXL", "S"]);
        var sourceFiles = new YandexDiskSourceFiles(new ApiClient("token", new HttpClient(handler)), new YandexDiskSettings(),
            NullLogger.Instance);

        // Act
        var preview = await sourceFiles.DownloadPreviewAsync("disk:/Photos/IMG_0001.jpg", CancellationToken.None);

        // Assert
        Assert.Equal("disk:/Photos/IMG_0001.jpg", handler.ResourcePath);
        Assert.Equal("https://preview.test/XXXL", handler.DownloadedUrl);
        Assert.Equal([9, 9], preview!.Contents);
        Assert.Equal("image/jpeg", preview.ContentType);
    }

    [Fact]
    public async Task YandexDiskSourceFiles_DownloadPreviewAsync_ShouldTakeTheLargestPreview_WhenThereIsNoXxxlPreview()
    {
        // Arrange
        var handler = new FakePreviewHandler(["S", "XL", "M"]);
        var sourceFiles = new YandexDiskSourceFiles(new ApiClient("token", new HttpClient(handler)), new YandexDiskSettings(),
            NullLogger.Instance);

        // Act
        await sourceFiles.DownloadPreviewAsync("disk:/Photos/IMG_0001.jpg", CancellationToken.None);

        // Assert
        Assert.Equal("https://preview.test/XL", handler.DownloadedUrl);
    }

    [Fact]
    public async Task YandexDiskSourceFiles_DownloadPreviewAsync_ShouldBeNull_WhenThereIsNoPreview()
    {
        // Arrange
        var sourceFiles = new YandexDiskSourceFiles(new ApiClient("token", new HttpClient(new FakePreviewHandler([]))),
            new YandexDiskSettings(), NullLogger.Instance);

        // Act & Assert
        Assert.Null(await sourceFiles.DownloadPreviewAsync("disk:/Photos/IMG_0001.jpg", CancellationToken.None));
    }

    private FailedPreviewsDownloader CreateDownloader()
    {
        return new FailedPreviewsDownloader(_failedPreviewService.Object, _photoSourceService.Object, _userPhotoSourceService.Object,
            _sourceFilesFactory.Object, _fileStorage.Object, NullLogger<FailedPreviewsDownloader>.Instance);
    }

    /// <summary>
    /// The previews to download in the first round, none in the next one.
    /// </summary>
    private void Pending(params FailedPreview[] failedPreviews)
    {
        _failedPreviewService
            .SetupSequence(a => a.GetPendingAsync(It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>()))
            .ReturnsAsync(failedPreviews)
            .ReturnsAsync([]);
    }

    private static FailedPreview CreateFailedPreview(FailedPreviewTable table, long id, string reference)
    {
        return new FailedPreview
        {
            Table = table,
            Id = id,
            UserId = UserId,
            PhotoSourceId = SourceId,
            FileName = Path.GetFileName(reference),
            Reference = reference,
            Error = "The connection was reset.",
            FailedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// A file of Yandex.Disk with previews of the sizes given, and the download of a preview.
    /// </summary>
    private sealed class FakePreviewHandler(string[] sizeNames) : HttpMessageHandler
    {
        public string? ResourcePath { get; private set; }
        public string? DownloadedUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;

            if (uri.Host == "preview.test")
            {
                DownloadedUrl = uri.ToString();

                var preview = new ByteArrayContent([9, 9]);
                preview.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = preview });
            }

            ResourcePath = System.Web.HttpUtility.ParseQueryString(uri.Query)["path"];

            var sizes = string.Join(",", sizeNames.Select(a => $$"""{"name":"{{a}}","url":"https://preview.test/{{a}}"}"""));
            var json = $$"""{"name":"IMG_0001.jpg","path":"disk:/Photos/IMG_0001.jpg","type":"file","resource_id":"r1","sizes":[{{sizes}}]}""";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
