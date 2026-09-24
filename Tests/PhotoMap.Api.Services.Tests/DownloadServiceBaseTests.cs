using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Tests;

public class DownloadServiceBaseTests
{
    private const long UserId = 1;
    private const long SourceId = 2;

    private readonly Mock<IFailedFileService> _failedFileService = new();
    private readonly ProcessingProgress _progress = new(0, 0);

    [Fact]
    public async Task DownloadAsync_ShouldCountAndRecordFileThatFailsToDownload_AndGoOn()
    {
        // Arrange
        var service = CreateService(new Dictionary<string, Exception?>
        {
            ["ok"] = null,
            ["broken"] = new DropboxException("Not found.")
        });

        // Act
        var downloadedFiles = await ProcessAllAsync(service.DownloadAsync(CancellationToken.None));

        // Assert
        Assert.Equal(["ok"], downloadedFiles);
        Assert.Equal(1, _progress.FailedCount);

        _failedFileService.Verify(a => a.RecordAsync(UserId, SourceId,
            It.Is<IReadOnlyCollection<FailedFile>>(b => b.Single().ExternalId == "broken" &&
                                                        b.Single().Stage == FileFailureStage.Download),
            It.Is<IReadOnlyCollection<string>>(b => b.SequenceEqual(new[] { "ok" }))));
    }

    [Fact]
    public async Task DownloadAsync_ShouldFail_WhenTheAccessTokenIsRejected()
    {
        // Arrange
        var service = CreateService(new Dictionary<string, Exception?>
        {
            ["expired"] = new DropboxException("Access token has expired.", isAuthError: true)
        });

        // Act, Assert: every other file would fail as well
        await Assert.ThrowsAsync<DropboxException>(() => ProcessAllAsync(service.DownloadAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task DownloadAsync_ShouldNotRecordThePage_WhenStoppedBeforeItIsProcessed()
    {
        // Arrange
        var service = CreateService(new Dictionary<string, Exception?> { ["ok"] = null });
        using var cancellation = new CancellationTokenSource();

        // Act: the file is not processed before the run is stopped
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in service.DownloadAsync(cancellation.Token))
            {
                await cancellation.CancelAsync();
            }
        });

        // Assert
        Assert.False(service.PageCompleted);
        _failedFileService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RetryFailedAsync_ShouldRecordFilesThatFailAgain_WithoutCountingThemAgain()
    {
        // Arrange
        _failedFileService
            .Setup(a => a.GetAsync(UserId, SourceId))
            .ReturnsAsync([CreateFailedFile("recovered"), CreateFailedFile("still-broken")]);

        var service = CreateService(new Dictionary<string, Exception?>
        {
            ["recovered"] = null,
            ["still-broken"] = new YandexDiskException("Not found.")
        });

        // Act
        var downloadedFiles = await ProcessAllAsync(service.RetryFailedAsync(CancellationToken.None));

        // Assert
        Assert.Equal(["recovered"], downloadedFiles);
        Assert.Equal(0, _progress.FailedCount);

        _failedFileService.Verify(a => a.RecordAsync(UserId, SourceId,
            It.Is<IReadOnlyCollection<FailedFile>>(b => b.Single().ExternalId == "still-broken"),
            It.Is<IReadOnlyCollection<string>>(b => b.SequenceEqual(new[] { "recovered" }))));
    }

    /// <summary>
    /// Downloads the files, processing each one as it comes.
    /// </summary>
    private static async Task<List<string>> ProcessAllAsync(IAsyncEnumerable<DownloadedFile> downloadedFiles)
    {
        var fileIds = new List<string>();

        await foreach (var downloadedFile in downloadedFiles)
        {
            fileIds.Add(downloadedFile.FileInfo.FileId);
            downloadedFile.Processed.SetResult(ProcessingResult.Success);
        }

        return fileIds;
    }

    private FakeDownloadService CreateService(Dictionary<string, Exception?> files)
    {
        var parameters = new DownloadServiceParameters
        {
            UserId = UserId,
            SourceId = SourceId,
            AuthResult = new UserAuthResult { Token = "token", TokenExpiresOn = DateTimeOffset.UtcNow.AddHours(1) },
            ClientId = "client-id",
            Progress = _progress
        };

        return new FakeDownloadService(files, new Mock<IDownloadStateService<FakeState>>().Object,
            _failedFileService.Object, parameters);
    }

    private static FailedFile CreateFailedFile(string externalId)
    {
        return new FailedFile { ExternalId = externalId, FileName = externalId + ".jpg", Error = "Failed." };
    }

    public sealed class FakeState;

    /// <summary>
    /// A source of one page of the given files, each downloaded or failing with the exception given for it.
    /// </summary>
    private sealed class FakeDownloadService : DownloadServiceBase<FakeState>
    {
        private readonly Dictionary<string, Exception?> _files;

        public FakeDownloadService(Dictionary<string, Exception?> files, IDownloadStateService<FakeState> stateService,
            IFailedFileService failedFileService, DownloadServiceParameters parameters)
            : base(NullLogger.Instance, stateService, new Mock<IPhotoService>().Object, failedFileService, parameters)
        {
            _files = files;
        }

        public bool PageCompleted { get; private set; }

        protected override int PageSize => 10;
        protected override int MaxParallelDownloads => 2;

        public override async IAsyncEnumerable<DownloadedFile> DownloadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var file in DownloadPageAsync(_files.Keys.ToList(),
                               (id, _) => DownloadOrSkipAsync(() => DownloadAsync(id), id, null, id + ".jpg"),
                               cancellationToken))
            {
                yield return file;
            }

            PageCompleted = true;
        }

        public override Task<byte[]> DownloadFileAsync(string? externalId, string? path, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override Task<int> GetTotalFileCountAsync()
        {
            return Task.FromResult(_files.Count);
        }

        protected override void CreateClient()
        {
        }

        protected override Task<DownloadedFile> RetryFileAsync(FailedFile failedFile, CancellationToken cancellationToken)
        {
            return DownloadAsync(failedFile.ExternalId);
        }

        private Task<DownloadedFile> DownloadAsync(string id)
        {
            if (_files[id] is { } exception)
            {
                return Task.FromException<DownloadedFile>(exception);
            }

            return Task.FromResult(new DownloadedFile(new DownloadedFileInfo(id + ".jpg", "/" + id + ".jpg", null, id), []));
        }
    }
}
