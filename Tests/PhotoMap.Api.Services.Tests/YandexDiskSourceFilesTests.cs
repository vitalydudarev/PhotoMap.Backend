using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging.Abstractions;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Yandex.Disk;

namespace PhotoMap.Api.Services.Tests;

public class YandexDiskSourceFilesTests
{
    /// <summary>
    /// A disk of folders within folders, a folder by its path with the names of the files and folders in it.
    /// </summary>
    private static readonly Dictionary<string, string[]> Disk = new()
    {
        ["disk:/Photos"] = ["IMG_0001.jpg", "2020/", "notes.txt"],
        ["disk:/Photos/2020"] = ["IMG_0002.heic", "Trip/", "Empty/"],
        ["disk:/Photos/2020/Trip"] = ["VID_0003.mp4", "Day 1/"],
        ["disk:/Photos/2020/Trip/Day 1"] = ["IMG_0004.jpg"],
        ["disk:/Photos/2020/Empty"] = []
    };

    [Fact]
    public async Task ListAsync_ShouldListTheFilesOfTheSubfoldersAtAnyDepth_WhenAskedTo()
    {
        // Arrange
        var handler = new FakeDiskHandler();
        var sourceFiles = CreateSourceFiles(handler);

        // Act
        var files = await sourceFiles.ListAsync(SourceDownloadContent.All, "disk:/Photos", includeSubfolders: true,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            ["disk:/Photos/IMG_0001.jpg", "disk:/Photos/2020/IMG_0002.heic", "disk:/Photos/2020/Trip/VID_0003.mp4",
                "disk:/Photos/2020/Trip/Day 1/IMG_0004.jpg"],
            files.Select(a => a.Path));
        Assert.Equal(SourceDownloadContent.Videos, files.Single(a => a.Name == "VID_0003.mp4").Content);
        Assert.Equal(Disk.Keys.Order(), handler.ListedFolders.Order());
    }

    [Fact]
    public async Task ListAsync_ShouldListTheFilesOfTheFolderOnly_WhenNotAskedForSubfolders()
    {
        // Arrange
        var handler = new FakeDiskHandler();
        var sourceFiles = CreateSourceFiles(handler);

        // Act
        var files = await sourceFiles.ListAsync(SourceDownloadContent.All, "disk:/Photos", includeSubfolders: false,
            CancellationToken.None);

        // Assert
        Assert.Equal(["disk:/Photos/IMG_0001.jpg"], files.Select(a => a.Path));
        Assert.Equal(["disk:/Photos"], handler.ListedFolders);
    }

    [Fact]
    public async Task ListAsync_ShouldListTheContentChosen_InTheSubfoldersAsWell()
    {
        // Arrange
        var sourceFiles = CreateSourceFiles(new FakeDiskHandler());

        // Act
        var files = await sourceFiles.ListAsync(SourceDownloadContent.Videos, "disk:/Photos", includeSubfolders: true,
            CancellationToken.None);

        // Assert
        Assert.Equal(["VID_0003.mp4"], files.Select(a => a.Name));
    }

    private static YandexDiskSourceFiles CreateSourceFiles(FakeDiskHandler handler)
    {
        return new YandexDiskSourceFiles(new ApiClient("token", new HttpClient(handler)), new YandexDiskSettings(),
            NullLogger.Instance);
    }

    /// <summary>
    /// Answers the listing of a folder of <see cref="Disk"/>, a name ending in a slash being a folder.
    /// </summary>
    private sealed class FakeDiskHandler : HttpMessageHandler
    {
        public List<string> ListedFolders { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = HttpUtility.ParseQueryString(request.RequestUri!.Query)["path"]!;
            ListedFolders.Add(path);

            var items = Disk[path].Select(name => name.EndsWith('/')
                ? new { name = name.TrimEnd('/'), path = $"{path}/{name.TrimEnd('/')}", type = "dir", resource_id = name, size = 0L }
                : new { name, path = $"{path}/{name}", type = "file", resource_id = name, size = 10L }).ToArray();

            var json = JsonSerializer.Serialize(new
            {
                name = path,
                path,
                type = "dir",
                resource_id = path,
                _embedded = new { items, limit = 10000, offset = 0, path, total = items.Length }
            });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
