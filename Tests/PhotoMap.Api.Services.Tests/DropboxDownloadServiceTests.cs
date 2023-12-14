using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Models;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class DropboxDownloadServiceTests
{
    [Fact]
    public async Task Test1()
    {
        var stateService = new Mock<IDropboxDownloadStateService>();
        stateService.Setup(a => a.GetStateAsync(It.IsAny<long>(), It.IsAny<long>())).ReturnsAsync((DropboxDownloadState?)null);

        var dropboxDownloadService = new DropboxDownloadService(NullLogger<DropboxDownloadService>.Instance, stateService.Object, null,
            new DropboxSettings() { DownloadLimit = 2000, SourceFolder = "/Camera Uploads" });

        // var dropboxUserIdentifier = new DropboxUserIdentifier { UserId = 1 };
        var apiToken = "sl.BfTz67hm63XYPQFMxPWCX4mKwu63hq_XWQa52aFcT3lgGIf1Fp-RQ96qX8juHRrsL7sON5K6DuOvAqZTAdy1sx16Q2GbrKUJQjCupS-R3p5Ph96dybY3fAutNSgE7Aj4w7wbLvg";
        
        await foreach (var downloadedFileInfo in dropboxDownloadService.DownloadAsync(
                           1,
                           1,
                           apiToken,
                           new CancellationToken()))
        {
            
        }
    }
}