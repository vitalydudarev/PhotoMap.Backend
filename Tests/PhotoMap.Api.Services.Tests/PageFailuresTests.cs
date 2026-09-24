using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Tests;

public class PageFailuresTests
{
    private const long UserId = 1;
    private const long SourceId = 2;

    [Fact]
    public async Task RecordAsync_ShouldRecordTheFailedFilesAndTheSavedOnes()
    {
        // Arrange
        var failedFileService = new Mock<IFailedFileService>();
        IReadOnlyCollection<FailedFile>? recordedFailedFiles = null;
        IReadOnlyCollection<string>? recordedSavedExternalIds = null;
        failedFileService
            .Setup(a => a.RecordAsync(UserId, SourceId, It.IsAny<IReadOnlyCollection<FailedFile>>(),
                It.IsAny<IReadOnlyCollection<string>>()))
            .Callback<long, long, IReadOnlyCollection<FailedFile>, IReadOnlyCollection<string>>((_, _, a, b) =>
            {
                recordedFailedFiles = a;
                recordedSavedExternalIds = b;
            })
            .Returns(Task.CompletedTask);

        var pageFailures = new PageFailures();
        pageFailures.DownloadFailed("id:download", "/download.jpg", "download.jpg", "Timed out");

        var savedFile = CreateProcessedFile("id:saved", ProcessingResult.Success);
        var failedFile = CreateProcessedFile("id:processing", ProcessingResult.Failed("Corrupt image"));

        // Act
        await pageFailures.RecordAsync(failedFileService.Object, UserId, SourceId, [savedFile, failedFile]);

        // Assert
        Assert.NotNull(recordedFailedFiles);
        Assert.Collection(recordedFailedFiles.OrderBy(a => a.ExternalId),
            a =>
            {
                Assert.Equal("id:download", a.ExternalId);
                Assert.Equal(FileFailureStage.Download, a.Stage);
                Assert.Equal("Timed out", a.Error);
            },
            a =>
            {
                Assert.Equal("id:processing", a.ExternalId);
                Assert.Equal("/id:processing.jpg", a.Path);
                Assert.Equal(FileFailureStage.Processing, a.Stage);
                Assert.Equal("Corrupt image", a.Error);
            });

        Assert.Equal(["id:saved"], recordedSavedExternalIds);
    }

    private static DownloadedFile CreateProcessedFile(string externalId, ProcessingResult result)
    {
        var fileInfo = new DownloadedFileInfo(externalId + ".jpg", "/" + externalId + ".jpg", null, externalId);
        var file = new DownloadedFile(fileInfo, []);
        file.Processed.SetResult(result);

        return file;
    }
}
