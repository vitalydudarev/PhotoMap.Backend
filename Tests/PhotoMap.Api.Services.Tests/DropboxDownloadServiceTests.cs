using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class DropboxDownloadServiceTests
{
    [Fact(Skip = "Manual test, calls the Dropbox API and needs a valid access token")]
    public async Task Test1()
    {
        var apiToken = "sl.BfTz67hm63XYPQFMxPWCX4mKwu63hq_XWQa52aFcT3lgGIf1Fp-RQ96qX8juHRrsL7sON5K6DuOvAqZTAdy1sx16Q2GbrKUJQjCupS-R3p5Ph96dybY3fAutNSgE7Aj4w7wbLvg";
        var authResult = new UserAuthResult { Token = apiToken, TokenExpiresOn = DateTimeOffset.UtcNow.AddHours(1) };

        var stateService = new Mock<IDropboxDownloadStateService>();
        stateService.Setup(a => a.GetStateAsync(It.IsAny<long>(), It.IsAny<long>())).ReturnsAsync((DropboxDownloadState?)null);

        var dropboxDownloadService = new DropboxDownloadService(NullLogger<DropboxDownloadService>.Instance, stateService.Object, null, new Mock<IPhotoService>().Object,
            new DropboxSettings() { DownloadLimit = 2000, SourceFolder = "/Camera Uploads" }, new DownloadServiceParameters { AuthResult = authResult, ClientId = "8pakfnac86x0iad", Progress = new ProcessingProgress(0, 0) });

        await foreach (var downloadedFileInfo in dropboxDownloadService.DownloadAsync(new CancellationToken()))
        {
            
        }
    }
}