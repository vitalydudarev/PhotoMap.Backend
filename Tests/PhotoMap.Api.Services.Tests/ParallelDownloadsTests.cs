using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Tests;

public class ParallelDownloadsTests
{
    private const int MaxDegreeOfParallelism = 4;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task RunAsync_ShouldReturnEveryDownloadedFile()
    {
        // Arrange
        var files = Enumerable.Range(0, 20).Select(a => a.ToString()).ToList();

        // Act
        var downloadedFiles = await ReadAllAsync(files, (file, _) => Task.FromResult(Download(file))!);

        // Assert
        Assert.Equal(files.Order(), downloadedFiles.Select(a => a.FileInfo.ResourceName).Order());
    }

    [Fact]
    public async Task RunAsync_ShouldDownloadSeveralFilesAtATime()
    {
        // Arrange: every download blocks until all of them are in flight, which only happens in parallel
        using var filesInDownload = new CountdownEvent(MaxDegreeOfParallelism);

        var files = Enumerable.Range(0, MaxDegreeOfParallelism).Select(a => a.ToString()).ToList();

        // Act
        var downloadedFiles = await ReadAllAsync(files, async (file, token) =>
        {
            filesInDownload.Signal();
            await Task.Run(() => filesInDownload.Wait(Timeout), token);

            return Download(file);
        });

        // Assert
        Assert.True(filesInDownload.IsSet, $"{MaxDegreeOfParallelism} files were expected to be downloaded at a time");
        Assert.Equal(files.Count, downloadedFiles.Count);
    }

    [Fact]
    public async Task RunAsync_ShouldSkipFilesWithoutContents()
    {
        // Arrange
        var files = new List<string> { "downloaded", "skipped" };

        // Act
        var downloadedFiles = await ReadAllAsync(files,
            (file, _) => Task.FromResult(file == "skipped" ? null : Download(file)));

        // Assert
        Assert.Equal("downloaded", Assert.Single(downloadedFiles).FileInfo.ResourceName);
    }

    [Fact]
    public async Task RunAsync_ShouldReportFailedDownload()
    {
        // Arrange
        var files = new List<string> { "failing" };

        // Act, Assert: the run has to fail, so that it is not recorded as done and resumes from the same page
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReadAllAsync(files, (_, _) => throw new InvalidOperationException("Access token has expired.")));

        Assert.Equal("Access token has expired.", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ShouldStopDownloading_WhenCancelled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        var files = Enumerable.Range(0, 100).Select(a => a.ToString()).ToList();
        var startedDownloads = 0;

        // Act, Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var downloadedFile in ParallelDownloads.RunAsync(files, MaxDegreeOfParallelism,
                               async (file, token) =>
                               {
                                   Interlocked.Increment(ref startedDownloads);
                                   await Task.Delay(TimeSpan.FromMilliseconds(10), token);

                                   return Download(file);
                               }, cancellationTokenSource.Token))
            {
                Assert.NotNull(downloadedFile);

                await cancellationTokenSource.CancelAsync();
            }
        });

        // the files left of the page are downloaded again when the run is resumed
        Assert.True(startedDownloads < files.Count, "the remaining files were expected not to be downloaded");
    }

    [Fact]
    public async Task RunAsync_ShouldStopDownloading_WhenCallerStopsReading()
    {
        // Arrange
        var files = Enumerable.Range(0, 100).Select(a => a.ToString()).ToList();
        var startedDownloads = 0;

        // Act
        await foreach (var downloadedFile in ParallelDownloads.RunAsync(files, MaxDegreeOfParallelism,
                           async (file, token) =>
                           {
                               Interlocked.Increment(ref startedDownloads);
                               await Task.Delay(TimeSpan.FromMilliseconds(10), token);

                               return Download(file);
                           }, CancellationToken.None))
        {
            Assert.NotNull(downloadedFile);

            break;
        }

        // Assert: the downloads are not left running in the background
        var startedWhenCallerStopped = startedDownloads;
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        Assert.Equal(startedWhenCallerStopped, startedDownloads);
    }

    private static async Task<List<DownloadedFile>> ReadAllAsync(
        IReadOnlyCollection<string> files,
        Func<string, CancellationToken, Task<DownloadedFile?>> downloadAsync)
    {
        using var cancellationTokenSource = new CancellationTokenSource(Timeout);

        var downloadedFiles = new List<DownloadedFile>();

        await foreach (var downloadedFile in ParallelDownloads.RunAsync(files, MaxDegreeOfParallelism, downloadAsync,
                           cancellationTokenSource.Token))
        {
            downloadedFiles.Add(downloadedFile);
        }

        return downloadedFiles;
    }

    private static DownloadedFile Download(string fileName)
    {
        return new DownloadedFile(new DownloadedFileInfo(fileName, "/" + fileName, DateTime.UtcNow, "id:" + fileName),
            [1, 2, 3]);
    }
}
