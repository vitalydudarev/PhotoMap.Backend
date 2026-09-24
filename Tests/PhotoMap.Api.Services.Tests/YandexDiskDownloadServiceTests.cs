using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Tests;

public class YandexDiskDownloadServiceTests
{
    private const long UserId = 1;
    private const long SourceId = 2;
    private const int FileCount = 5;

    private readonly Mock<IYandexDiskDownloadStateService> _stateService = new();
    private readonly List<int> _savedOffsets = [];

    public YandexDiskDownloadServiceTests()
    {
        _stateService
            .Setup(a => a.SaveStateAsync(UserId, SourceId, It.IsAny<YandexDiskDownloadState>()))
            .Callback<long, long, YandexDiskDownloadState>((_, _, a) => _savedOffsets.Add(a.Offset))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task DownloadAsync_ShouldSaveTheOffsetAfterEveryChunkOfAPage()
    {
        // Arrange
        SetUpSavedOffset(null);
        await using var service = CreateService(pageSize: 10, saveStateEvery: 2);

        // Act
        var downloadedFiles = await DownloadAllAsync(service, CancellationToken.None);

        // Assert
        Assert.Equal(["r0", "r1", "r2", "r3", "r4"], downloadedFiles.Order());
        Assert.Equal([2, 4, 5], _savedOffsets);
    }

    [Fact]
    public async Task DownloadAsync_ShouldContinueFromTheSavedOffset()
    {
        // Arrange
        SetUpSavedOffset(4);
        await using var service = CreateService(pageSize: 10, saveStateEvery: 2);

        // Act
        var downloadedFiles = await DownloadAllAsync(service, CancellationToken.None);

        // Assert
        Assert.Equal(["r4"], downloadedFiles);
        Assert.Equal([5], _savedOffsets);
    }

    [Fact]
    public async Task DownloadAsync_ShouldNotSaveTheOffsetPastFilesNotProcessed_WhenStopped()
    {
        // Arrange
        SetUpSavedOffset(null);
        await using var service = CreateService(pageSize: 10, saveStateEvery: 2);
        using var cancellation = new CancellationTokenSource();

        // Act: the files of the first chunk are processed, the first file of the second one is not
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var downloadedFile in service.DownloadAsync(cancellation.Token))
            {
                if (downloadedFile.FileInfo.FileId is "r0" or "r1")
                {
                    downloadedFile.Processed.SetResult(ProcessingResult.Success);
                }
                else
                {
                    await cancellation.CancelAsync();
                }
            }
        });

        // Assert
        Assert.Equal([2], _savedOffsets);
    }

    private void SetUpSavedOffset(int? offset)
    {
        _stateService
            .Setup(a => a.GetStateAsync(UserId, SourceId))
            .ReturnsAsync(offset == null ? null : new YandexDiskDownloadState { Offset = offset.Value });
    }

    /// <summary>
    /// Downloads the files, processing each one as it comes.
    /// </summary>
    private static async Task<List<string>> DownloadAllAsync(YandexDiskDownloadService service,
        CancellationToken cancellationToken)
    {
        var fileIds = new List<string>();

        await foreach (var downloadedFile in service.DownloadAsync(cancellationToken))
        {
            fileIds.Add(downloadedFile.FileInfo.FileId);
            downloadedFile.Processed.SetResult(ProcessingResult.Success);
        }

        return fileIds;
    }

    private YandexDiskDownloadService CreateService(int pageSize, int saveStateEvery)
    {
        var photoService = new Mock<IPhotoService>();
        photoService
            .Setup(a => a.GetSavedExternalIdsAsync(UserId, SourceId, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new HashSet<string>());

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(a => a.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new FakeYandexDiskHandler()));

        var settings = new YandexDiskSettings
        {
            UsePhotoStreamFolder = false,
            SourceFolder = "disk:/Photos",
            DownloadLimit = pageSize,
            SaveStateEvery = saveStateEvery,
            MaxParallelDownloads = 2
        };

        var parameters = new DownloadServiceParameters
        {
            UserId = UserId,
            SourceId = SourceId,
            AuthResult = new UserAuthResult { Token = "token", TokenExpiresOn = DateTimeOffset.UtcNow.AddHours(1) },
            ClientId = "client-id",
            Progress = new ProcessingProgress(0, 0)
        };

        return new YandexDiskDownloadService(NullLogger<YandexDiskDownloadService>.Instance, _stateService.Object,
            photoService.Object, new Mock<IFailedFileService>().Object, httpClientFactory.Object, settings, parameters);
    }

    /// <summary>
    /// A folder of <see cref="FileCount"/> photos, listed by offset and limit, and the downloads of its files.
    /// </summary>
    private sealed class FakeYandexDiskHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var query = HttpUtility.ParseQueryString(uri.Query);

            if (uri.Host == "downloader.test")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
            }

            if (uri.AbsolutePath.EndsWith("/resources/download"))
            {
                var name = query["path"]!.Split('/').Last();

                return Json(new { href = $"https://downloader.test/{name}", method = "GET", templated = false });
            }

            var offset = int.Parse(query["offset"]!);
            var limit = int.Parse(query["limit"]!);

            var items = Enumerable.Range(0, FileCount).Skip(offset).Take(limit).Select(a => new
            {
                name = $"photo{a}.jpg",
                path = $"disk:/Photos/photo{a}.jpg",
                type = "file",
                resource_id = $"r{a}",
                created = new DateTime(2020, 1, 1, 0, 0, a, DateTimeKind.Utc)
            });

            return Json(new
            {
                name = "Photos",
                path = "disk:/Photos",
                type = "dir",
                _embedded = new { items, offset, limit, total = FileCount, path = "disk:/Photos" }
            });
        }

        private static Task<HttpResponseMessage> Json(object value)
        {
            var content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
