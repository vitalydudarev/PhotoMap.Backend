using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using static PhotoMap.Api.Services.Tests.VideoExifTests;

namespace PhotoMap.Api.Services.Tests;

public class VideoExifBackfillerTests
{
    private const long UserId = 1;
    private const long YandexDiskSourceId = 2;
    private const long DropboxSourceId = 3;

    private readonly Mock<IVideoService> _videoService = new();
    private readonly Mock<IVideoProcessingService> _videoProcessingService = new();
    private readonly Mock<IUserPhotoSourceService> _userPhotoSourceService = new();
    private readonly Dictionary<long, VideoExif> _savedExif = new();

    private readonly QuickTimeFile _file = QuickTimeFile.Create(mdatSize: 1024 * 1024, new Dictionary<string, string>
    {
        ["com.apple.quicktime.location.ISO6709"] = "+53.9095+027.5202+224.391/"
    });

    public VideoExifBackfillerTests()
    {
        _videoProcessingService.Setup(a => a.SupportsVideosAsync(YandexDiskSourceId)).ReturnsAsync(true);
        _videoProcessingService.Setup(a => a.SupportsVideosAsync(DropboxSourceId)).ReturnsAsync(false);
        _userPhotoSourceService
            .Setup(a => a.GetAuthResultAsync(UserId, YandexDiskSourceId))
            .ReturnsAsync(new UserAuthResult { Token = "token", TokenExpiresOn = DateTimeOffset.UtcNow.AddDays(1) });
        _videoService
            .Setup(a => a.UpdateExifAsync(It.IsAny<IReadOnlyDictionary<long, VideoExif>>()))
            .Callback<IReadOnlyDictionary<long, VideoExif>>(a =>
            {
                foreach (var (id, exif) in a)
                {
                    _savedExif[id] = exif;
                }
            })
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ReadAllAsync_ShouldReadAndSaveTheMetadataOfTheVideos_ByTheirPaths()
    {
        // Arrange
        SetUpVideos(CreateVideo(1, "disk:/Camera Uploads", "a.mov"), CreateVideo(2, "disk:/", "b.mov"));
        var handler = new RangeHandler(_file);

        // Act
        var result = await CreateBackfiller(handler).ReadAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(new VideoExifBackfill(2, 0, 0), result);
        Assert.Equal([1L, 2L], _savedExif.Keys.Order());
        Assert.All(_savedExif.Values, a => Assert.Equal(53.9095, a.Latitude));
        Assert.Equal(["disk:/Camera Uploads/a.mov", "disk:/b.mov"], handler.DownloadUrlPaths.Order(StringComparer.Ordinal));
        Assert.Equal(0, handler.WholeDownloadCount);
    }

    [Fact]
    public async Task ReadAllAsync_ShouldDownloadTheWholeVideo_WhenTheServerDoesNotSendParts()
    {
        // Arrange
        SetUpVideos(CreateVideo(1, "disk:/Camera Uploads", "a.mov"));
        var handler = new RangeHandler(_file) { IgnoresRanges = true };

        // Act
        var result = await CreateBackfiller(handler).ReadAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(new VideoExifBackfill(1, 0, 0), result);
        Assert.Equal(53.9095, _savedExif[1].Latitude);
    }

    [Fact]
    public async Task ReadAllAsync_ShouldSkipTheVideos_OfASourceThatCannotBeDownloadedFrom()
    {
        // Arrange: Dropbox has no videos processed, and the user is not authorized in the other Yandex.Disk
        SetUpVideos(
            CreateVideo(1, "disk:/", "a.mov", DropboxSourceId),
            CreateVideo(2, "disk:/", "b.mov", photoSourceId: 4),
            CreateVideo(3, null, "c.mov"));
        _videoProcessingService.Setup(a => a.SupportsVideosAsync(4)).ReturnsAsync(true);
        var handler = new RangeHandler(_file);

        // Act
        var result = await CreateBackfiller(handler).ReadAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(new VideoExifBackfill(0, 0, 3), result);
        Assert.Empty(_savedExif);
        Assert.Equal(0, handler.DownloadUrlCount);
    }

    [Fact]
    public async Task ReadAllAsync_ShouldSkipTheVideos_OfASourceWhoseVideosAreBeingProcessed()
    {
        // Arrange: the run reads their metadata itself
        SetUpVideos(CreateVideo(1, "disk:/", "a.mov"));
        _videoProcessingService.Setup(a => a.IsRunning(UserId, YandexDiskSourceId)).Returns(true);

        // Act
        var result = await CreateBackfiller(new RangeHandler(_file)).ReadAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(new VideoExifBackfill(0, 0, 1), result);
    }

    [Fact]
    public async Task ReadAllAsync_ShouldLeaveTheVideosAsTheyAre_WhenTheTokenIsNotAccepted()
    {
        // Arrange
        SetUpVideos(CreateVideo(1, "disk:/", "a.mov"), CreateVideo(2, "disk:/", "b.mov"));

        // Act
        var result = await CreateBackfiller(new RangeHandler(_file) { Unauthorized = true }).ReadAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, result.ReadCount);
        Assert.Empty(_savedExif);
    }

    /// <summary>
    /// The videos without metadata, in a batch, then none.
    /// </summary>
    private void SetUpVideos(params Video[] videos)
    {
        _videoService.Setup(a => a.GetWithoutExifAsync(0, It.IsAny<int>())).ReturnsAsync(videos);
        var lastId = videos[^1].Id;
        _videoService.Setup(a => a.GetWithoutExifAsync(lastId, It.IsAny<int>())).ReturnsAsync([]);
    }

    private Video CreateVideo(long id, string? folderPath, string fileName, long photoSourceId = YandexDiskSourceId)
    {
        return new Video
        {
            Id = id,
            UserId = UserId,
            PhotoSourceId = photoSourceId,
            ExternalId = $"r{id}",
            FileName = fileName,
            FolderPath = folderPath,
            Size = _file.Length
        };
    }

    private VideoExifBackfiller CreateBackfiller(RangeHandler handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(a => a.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));

        return new VideoExifBackfiller(_videoService.Object, _videoProcessingService.Object, _userPhotoSourceService.Object,
            httpClientFactory.Object, NullLogger<VideoExifBackfiller>.Instance);
    }
}
