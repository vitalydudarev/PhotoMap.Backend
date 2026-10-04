using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging.Abstractions;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Yandex.Disk;

namespace PhotoMap.Api.Services.Tests;

public class YandexDiskVideoSourceTests
{
    [Fact]
    public async Task ListVideosAsync_ShouldListAVideoOnce_WhenItComesOnTwoPages()
    {
        // Arrange: the flat list is not sorted, the second page starts with the last video of the first one
        var pages = new Dictionary<int, string[]>
        {
            [0] = ["r1", "r2"],
            [2] = ["r2", "r3"],
            [4] = []
        };

        var apiClient = new ApiClient("token", new HttpClient(new FakeFilesHandler(pages)));
        var videoSource = new YandexDiskVideoSource(apiClient, NullLogger.Instance);

        // Act
        var videos = await videoSource.ListVideosAsync(CancellationToken.None);

        // Assert
        Assert.Equal(["r1", "r2", "r3"], videos.Select(a => a.ResourceId));
    }

    [Theory]
    [InlineData("disk:/Camera Uploads", "video.mp4", "disk:/Camera Uploads/video.mp4")]
    [InlineData("disk:/", "video.mp4", "disk:/video.mp4")]
    public void GetPath_ShouldPutTheFileNameInItsFolder(string folderPath, string fileName, string expectedPath)
    {
        Assert.Equal(expectedPath, YandexDiskVideoSource.GetPath(folderPath, fileName));
    }

    [Fact]
    public async Task OpenVideoAsync_ShouldDownloadTheRangeAskedFor_ByTheUrlOfThePath()
    {
        // Arrange
        var handler = new FakeDownloadHandler();
        var apiClient = new ApiClient("token", new HttpClient(handler));
        var videoSource = new YandexDiskVideoSource(apiClient, NullLogger.Instance);

        // Act
        using var response = await videoSource.OpenVideoAsync("disk:/Videos/trip.mp4", new RangeHeaderValue(100, null),
            CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("disk:/Videos/trip.mp4", handler.DownloadUrlPath);
        Assert.Equal("bytes=100-", handler.DownloadRange);
        Assert.Equal([1, 2, 3], await response.Content.ReadAsByteArrayAsync());
    }

    /// <summary>
    /// The download URL of a path, and the download of a range of the file by it.
    /// </summary>
    private sealed class FakeDownloadHandler : HttpMessageHandler
    {
        public string? DownloadUrlPath { get; private set; }
        public string? DownloadRange { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;

            if (uri.Host == "downloader.test")
            {
                DownloadRange = request.Headers.Range?.ToString();

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent([1, 2, 3])
                });
            }

            DownloadUrlPath = HttpUtility.ParseQueryString(uri.Query)["path"];

            var content = new StringContent(
                JsonSerializer.Serialize(new { href = "https://downloader.test/trip.mp4", method = "GET", templated = false }),
                Encoding.UTF8, "application/json");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    /// <summary>
    /// The flat list of videos, the resource IDs of its pages by their offset.
    /// </summary>
    private sealed class FakeFilesHandler(Dictionary<int, string[]> pages) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            var offset = int.Parse(query["offset"]!);

            var items = pages[offset].Select(a => new
            {
                name = $"{a}.mp4",
                path = $"disk:/Videos/{a}.mp4",
                type = "file",
                resource_id = a,
                created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });

            var content = new StringContent(JsonSerializer.Serialize(new { items, offset, limit = 500 }), Encoding.UTF8,
                "application/json");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
