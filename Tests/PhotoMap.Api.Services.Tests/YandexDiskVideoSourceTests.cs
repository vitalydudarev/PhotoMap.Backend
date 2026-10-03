using System.Net;
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
