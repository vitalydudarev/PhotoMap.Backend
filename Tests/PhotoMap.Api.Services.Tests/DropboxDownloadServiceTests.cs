using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class DropboxDownloadServiceTests
{
    [Fact(Skip = "Manual test, calls the Dropbox API with the access token in DROPBOX_ACCESS_TOKEN")]
    public async Task Test1()
    {
        var apiToken = Environment.GetEnvironmentVariable("DROPBOX_ACCESS_TOKEN")
            ?? throw new InvalidOperationException("Set DROPBOX_ACCESS_TOKEN to a valid Dropbox access token.");
        var authResult = new UserAuthResult { Token = apiToken, TokenExpiresOn = DateTimeOffset.UtcNow.AddHours(1) };

        var stateService = new Mock<IDownloadStateService<DropboxDownloadState>>();
        stateService.Setup(a => a.GetStateAsync(It.IsAny<long>(), It.IsAny<long>())).ReturnsAsync((DropboxDownloadState?)null);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(a => a.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient());

        var dropboxDownloadService = new DropboxDownloadService(NullLogger<DropboxDownloadService>.Instance, stateService.Object, new Mock<IPhotoService>().Object,
            new Mock<IFailedFileService>().Object,
            httpClientFactory.Object,
            new DropboxSettings { SourceFolder = "/Camera Uploads" }, new DownloadServiceParameters { AuthResult = authResult, ClientId = "8pakfnac86x0iad", Progress = new ProcessingProgress(0, 0) });

        await foreach (var downloadedFileInfo in dropboxDownloadService.DownloadAsync(new CancellationToken()))
        {
            
        }
    }
}