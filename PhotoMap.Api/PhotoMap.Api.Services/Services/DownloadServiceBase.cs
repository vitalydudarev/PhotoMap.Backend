using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// What downloading from a photo source takes whatever the source: the files of a page downloaded several at a
/// time, the ones that failed recorded once the page has been processed, retrying them, and the state a run resumes
/// from. The sources list and download their files their own way.
/// </summary>
/// <typeparam name="TState">What a run of the source saves to resume from.</typeparam>
public abstract class DownloadServiceBase<TState> : IDownloadService where TState : class, new()
{
    private readonly IDownloadStateService<TState> _stateService;
    private readonly IFailedFileService _failedFileService;
    private PageFailures _pageFailures = new();

    protected DownloadServiceBase(
        ILogger logger,
        IDownloadStateService<TState> stateService,
        IPhotoService photoService,
        IFailedFileService failedFileService,
        DownloadServiceParameters parameters)
    {
        Logger = logger;
        _stateService = stateService;
        PhotoService = photoService;
        _failedFileService = failedFileService;
        Parameters = parameters;
    }

    protected ILogger Logger { get; }
    protected IPhotoService PhotoService { get; }
    protected DownloadServiceParameters Parameters { get; }

    /// <summary>
    /// How many files are listed at a time, and how many failed files are retried at a time.
    /// </summary>
    protected abstract int PageSize { get; }

    /// <summary>
    /// How many files of a page are downloaded at a time.
    /// </summary>
    protected abstract int MaxParallelDownloads { get; }

    public abstract IAsyncEnumerable<DownloadedFile> DownloadAsync(CancellationToken cancellationToken);

    public abstract Task<byte[]> DownloadFileAsync(string? externalId, string? path, CancellationToken cancellationToken);

    public abstract Task<int> GetTotalFileCountAsync();

    public virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<DownloadedFile> RetryFailedAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        CreateClient();

        var failedFiles = await _failedFileService.GetAsync(Parameters.UserId, Parameters.SourceId);

        foreach (var page in failedFiles.Chunk(PageSize))
        {
            await foreach (var downloadedFile in DownloadPageAsync(page, RetryFileOrSkipAsync, cancellationToken))
            {
                yield return downloadedFile;
            }
        }
    }

    /// <summary>
    /// Creates the client of the API of the source, unless it has been created already.
    /// </summary>
    protected abstract void CreateClient();

    /// <summary>
    /// Downloads again a file that failed in an earlier run.
    /// </summary>
    /// <exception cref="PhotoSourceException">The file could not be downloaded.</exception>
    protected abstract Task<DownloadedFile> RetryFileAsync(FailedFile failedFile, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the files of one page, several at a time, and hands each one over as it arrives. They come in the
    /// order they finish downloading, which nothing depends on: once all of them have been processed, the files
    /// that failed are recorded and the caller saves the state the page has reached, however they were ordered.
    /// </summary>
    /// <param name="downloadOrSkipAsync">Downloads one file, returning null for a file that failed.</param>
    /// <exception cref="OperationCanceledException">The run is stopped before the page has been processed, the
    /// state of the page must not be saved then.</exception>
    protected async IAsyncEnumerable<DownloadedFile> DownloadPageAsync<T>(
        IReadOnlyCollection<T> files,
        Func<T, CancellationToken, Task<DownloadedFile?>> downloadOrSkipAsync,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pageFiles = new List<DownloadedFile>();
        _pageFailures = new PageFailures();

        await foreach (var downloadedFile in ParallelDownloads.RunAsync(files, Math.Max(MaxParallelDownloads, 1),
                           downloadOrSkipAsync, cancellationToken))
        {
            pageFiles.Add(downloadedFile);

            yield return downloadedFile;
        }

        await WaitUntilProcessedAsync(pageFiles, cancellationToken);

        await _pageFailures.RecordAsync(_failedFileService, Parameters.UserId, Parameters.SourceId, pageFiles);
    }

    /// <summary>
    /// Downloads a file listed in the source. A file that fails to download is counted and recorded as failed and
    /// skipped, unless the source no longer accepts the access token, which fails every other file as well.
    /// </summary>
    protected async Task<DownloadedFile?> DownloadOrSkipAsync(Func<Task<DownloadedFile>> downloadAsync, string externalId,
        string? path, string fileName)
    {
        try
        {
            return await downloadAsync();
        }
        catch (PhotoSourceException e) when (!e.IsAuthError)
        {
            // skip the file, the error has been logged
            Parameters.Progress.FileFailed();
            _pageFailures.DownloadFailed(externalId, path, fileName, e.Message);

            return null;
        }
    }

    protected async Task<TState> GetOrCreateStateAsync()
    {
        var state = await _stateService.GetStateAsync(Parameters.UserId, Parameters.SourceId);

        return state ?? new TState();
    }

    protected Task SaveStateAsync(TState state)
    {
        return _stateService.SaveStateAsync(Parameters.UserId, Parameters.SourceId, state);
    }

    /// <summary>
    /// The file has been counted as failed by the run it failed in, so failing again leaves the counters as they are.
    /// </summary>
    private async Task<DownloadedFile?> RetryFileOrSkipAsync(FailedFile failedFile, CancellationToken cancellationToken)
    {
        try
        {
            return await RetryFileAsync(failedFile, cancellationToken);
        }
        catch (PhotoSourceException e) when (!e.IsAuthError)
        {
            _pageFailures.DownloadFailed(failedFile.ExternalId, failedFile.Path, failedFile.FileName, e.Message);

            return null;
        }
    }

    private async Task WaitUntilProcessedAsync(List<DownloadedFile> files, CancellationToken cancellationToken)
    {
        try
        {
            var results = await Task.WhenAll(files.Select(a => a.Processed.Task)).WaitAsync(cancellationToken);

            var failedCount = results.Count(a => !a.Succeeded);
            if (failedCount > 0)
            {
                Logger.LogWarning("{FailedCount} of {FileCount} files of the page failed processing", failedCount, files.Count);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Cancellation requested");
            throw;
        }
    }
}
