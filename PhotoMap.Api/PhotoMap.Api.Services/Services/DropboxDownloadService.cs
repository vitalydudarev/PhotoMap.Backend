using System.Runtime.CompilerServices;
using Dropbox.Api;
using Dropbox.Api.Auth;
using Dropbox.Api.Files;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Shared.Models;
using DropboxException = PhotoMap.Api.Services.Exceptions.DropboxException;

namespace PhotoMap.Api.Services.Services;

public sealed class DropboxDownloadService : IDownloadService
{
    private const int MaxRateLimitRetries = 3;

    /// <summary>
    /// How many files are listed at a time. The cursor of a page is saved once the page has been processed, so
    /// this is how much of a stopped run is listed again when it resumes. Dropbox allows up to 2000, but a page
    /// that large is only finished at the end of a long run, which leaves nothing to resume from.
    /// </summary>
    private const int PageSize = 100;

    private readonly ILogger<DropboxDownloadService> _logger;
    private readonly IDropboxDownloadStateService _stateService;
    private readonly IPhotoService _photoService;
    private readonly IFailedFileService _failedFileService;
    private readonly DropboxSettings _settings;
    private DropboxClient? _dropboxClient;
    private readonly HttpClient _httpClient;
    private DropboxDownloadState? _state;
    private PageFailures _pageFailures = new();
    private readonly DownloadServiceParameters _parameters;

    public DropboxDownloadService(
        ILogger<DropboxDownloadService> logger,
        IDropboxDownloadStateService stateService,
        IPhotoService photoService,
        IFailedFileService failedFileService,
        IHttpClientFactory httpClientFactory,
        DropboxSettings settings,
        DownloadServiceParameters parameters)
    {
        _logger = logger;
        _stateService = stateService;
        _photoService = photoService;
        _failedFileService = failedFileService;
        _settings = settings;
        _parameters = parameters;
        _httpClient = httpClientFactory.CreateClient("dropboxClient");
    }

    #region Public Methods

    public async IAsyncEnumerable<DownloadedFile> DownloadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _state = await GetOrCreateStateAsync();

        CreateDropboxClient();

        // The cursor of a page is saved only after all of its files have been processed. After a restart the
        // unfinished page is listed again and the files saved in the meantime are skipped by their Dropbox file ID.
        var listFolderResult = await ListFolderAsync(_state.Cursor, cancellationToken);

        while (true)
        {
            var pageFiles = new List<DownloadedFile>();
            _pageFailures = new PageFailures();

            var filesToDownload = await GetFilesToDownloadAsync(listFolderResult);

            await foreach (var downloadedFile in DownloadPageAsync(filesToDownload, cancellationToken))
            {
                pageFiles.Add(downloadedFile);

                yield return downloadedFile;
            }

            if (!await WaitUntilProcessedAsync(pageFiles, cancellationToken))
            {
                _logger.LogInformation("Cancellation requested");
                yield break;
            }

            await RecordPageFailuresAsync(pageFiles);

            _state.Cursor = listFolderResult.Cursor;
            await SaveStateAsync();

            if (!listFolderResult.HasMore)
            {
                break;
            }

            listFolderResult = await ListFolderAsync(_state.Cursor, cancellationToken);
        }
    }

    public async IAsyncEnumerable<DownloadedFile> RetryFailedAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        CreateDropboxClient();

        var failedFiles = await _failedFileService.GetAsync(_parameters.UserId, _parameters.SourceId);

        foreach (var page in failedFiles.Chunk(PageSize))
        {
            var pageFiles = new List<DownloadedFile>();
            _pageFailures = new PageFailures();

            await foreach (var downloadedFile in ParallelDownloads.RunAsync(page, GetMaxParallelDownloads(),
                               RetryFileOrSkipAsync, cancellationToken))
            {
                pageFiles.Add(downloadedFile);

                yield return downloadedFile;
            }

            if (!await WaitUntilProcessedAsync(pageFiles, cancellationToken))
            {
                _logger.LogInformation("Cancellation requested");
                yield break;
            }

            await RecordPageFailuresAsync(pageFiles);
        }
    }

    public async Task<byte[]> DownloadFileAsync(string? externalId, string? path, CancellationToken cancellationToken)
    {
        // the file ID stays the same when the file is moved or renamed, the path doesn't
        var fileReference = externalId ?? path
            ?? throw new ArgumentException("Either the file ID or the path of the file must be given.", nameof(path));

        CreateDropboxClient();

        _logger.LogInformation("Started downloading {FileReference}", fileReference);

        using var response = await WrapApiCallAsync(() => _dropboxClient!.Files.DownloadAsync(fileReference), cancellationToken);

        await using var contentStream = await response.GetContentAsStreamAsync();
        using var memoryStream = new MemoryStream();

        await contentStream.CopyToAsync(memoryStream, cancellationToken);

        _logger.LogInformation("Finished downloading {FileReference}", fileReference);

        return memoryStream.ToArray();
    }

    public async Task<int> GetTotalFileCountAsync()
    {
        CreateDropboxClient();

        var totalCount = 0;
        string? cursor = null;

        do
        {
            var listFolderResult = cursor == null
                ? await WrapApiCallAsync(() => _dropboxClient!.Files.ListFolderAsync(_settings.SourceFolder, limit: PageSize))
                : await WrapApiCallAsync(() => _dropboxClient!.Files.ListFolderContinueAsync(cursor));

            totalCount += GetSupportedFiles(listFolderResult).Count();
            cursor = listFolderResult.HasMore ? listFolderResult.Cursor : null;
        } while (cursor != null);

        return totalCount;
    }

    public ValueTask DisposeAsync()
    {
        // the HTTP client is not disposed here, its handler is pooled and reused by IHttpClientFactory
        _dropboxClient?.Dispose();

        return ValueTask.CompletedTask;
    }
    
    #endregion Public Methods

    #region Private Methods

    private Task<ListFolderResult> ListFolderAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        if (cursor == null)
        {
            return WrapApiCallAsync(() => _dropboxClient!.Files.ListFolderAsync(_settings.SourceFolder, limit: PageSize), cancellationToken);
        }

        return WrapApiCallAsync(async () =>
        {
            try
            {
                return await _dropboxClient!.Files.ListFolderContinueAsync(cursor);
            }
            catch (ApiException<ListFolderContinueError> e) when (e.ErrorResponse.IsReset)
            {
                // Dropbox has invalidated the cursor, list the folder again (saved files are skipped)
                _logger.LogWarning("Dropbox cursor has been reset, listing the folder from the start");

                return await _dropboxClient!.Files.ListFolderAsync(_settings.SourceFolder, limit: PageSize);
            }
        }, cancellationToken);
    }

    private static IEnumerable<FileMetadata> GetSupportedFiles(ListFolderResult listFolderResult)
    {
        return listFolderResult.Entries.OfType<FileMetadata>().Where(a => SupportedImageFormats.IsSupported(a.Name));
    }

    /// <summary>
    /// The files of the page still to download. Saved files are looked up before the downloads start, in one
    /// query: the database context of the run is not thread safe, so the check cannot run alongside them.
    /// </summary>
    private async Task<IReadOnlyCollection<FileMetadata>> GetFilesToDownloadAsync(ListFolderResult listFolderResult)
    {
        var files = GetSupportedFiles(listFolderResult).ToList();

        var savedExternalIds = await _photoService.GetSavedExternalIdsAsync(_parameters.UserId, _parameters.SourceId,
            files.Select(a => a.Id));

        return files.Where(a => !savedExternalIds.Contains(a.Id)).ToList();
    }

    /// <summary>
    /// Downloads the files of one page, several at a time. They are handed over in the order they finish
    /// downloading, which nothing depends on: the cursor of the page is saved once all of them have been
    /// processed, however they were ordered.
    /// </summary>
    private IAsyncEnumerable<DownloadedFile> DownloadPageAsync(
        IReadOnlyCollection<FileMetadata> files,
        CancellationToken cancellationToken)
    {
        return ParallelDownloads.RunAsync(files, GetMaxParallelDownloads(), DownloadFileOrSkipAsync, cancellationToken);
    }

    private async Task<DownloadedFile?> DownloadFileOrSkipAsync(FileMetadata fileMetadata, CancellationToken cancellationToken)
    {
        try
        {
            return await DownloadFileWithInfoAsync(fileMetadata.Id, fileMetadata.Name, cancellationToken);
        }
        catch (DropboxException e) when (!e.IsAuthError)
        {
            // skip the file, the error has been logged
            _parameters.Progress.FileFailed();
            _pageFailures.DownloadFailed(fileMetadata.Id, fileMetadata.PathDisplay, fileMetadata.Name, e.Message);

            return null;
        }
    }

    /// <summary>
    /// The file is downloaded by its ID, which stays the same when it is moved or renamed. It has been counted as
    /// failed by the run it failed in, so failing again leaves the counters as they are.
    /// </summary>
    private async Task<DownloadedFile?> RetryFileOrSkipAsync(FailedFile failedFile, CancellationToken cancellationToken)
    {
        try
        {
            return await DownloadFileWithInfoAsync(failedFile.ExternalId, failedFile.FileName, cancellationToken);
        }
        catch (DropboxException e) when (!e.IsAuthError)
        {
            _pageFailures.DownloadFailed(failedFile.ExternalId, failedFile.Path, failedFile.FileName, e.Message);

            return null;
        }
    }

    private Task RecordPageFailuresAsync(IReadOnlyCollection<DownloadedFile> pageFiles)
    {
        return _pageFailures.RecordAsync(_failedFileService, _parameters.UserId, _parameters.SourceId, pageFiles);
    }

    private int GetMaxParallelDownloads()
    {
        return Math.Max(_settings.MaxParallelDownloads, 1);
    }

    private async Task<bool> WaitUntilProcessedAsync(List<DownloadedFile> files, CancellationToken cancellationToken)
    {
        try
        {
            var results = await Task.WhenAll(files.Select(a => a.Processed.Task)).WaitAsync(cancellationToken);

            var failedCount = results.Count(a => !a.Succeeded);
            if (failedCount > 0)
            {
                _logger.LogWarning("{FailedCount} of {FileCount} files of the page failed processing", failedCount, files.Count);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <param name="fileId">The Dropbox file ID of the file.</param>
    /// <param name="metadataName">The name of the file, for the log.</param>
    private async Task<DownloadedFile> DownloadFileWithInfoAsync(string fileId, string metadataName, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Started downloading {MetadataName}", metadataName);

            // the contents are read within the wrapped call: a failure reading them skips the file like any other
            // API error, rather than ending the run
            var (fileMetadata, fileContents) = await WrapApiCallAsync(async () =>
            {
                using var response = await _dropboxClient!.Files.DownloadAsync(fileId);

                return (response.Response, await response.GetContentAsByteArrayAsync());
            }, cancellationToken);

            _logger.LogInformation("Finished downloading {MetadataName}", metadataName);

            var fileInfo = new DownloadedFileInfo(fileMetadata.Name, fileMetadata.PathDisplay, fileMetadata.ClientModified,
                fileMetadata.Id);

            return new DownloadedFile(fileInfo, fileContents);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError("Failed downloading/saving {MetadataName}: {ErrorMessage}.", metadataName, e.Message);
            throw;
        }
    }
    
    private async Task<DropboxDownloadState> GetOrCreateStateAsync()
    {
        var state = await _stateService.GetStateAsync(_parameters.UserId, _parameters.SourceId);

        return state ?? new DropboxDownloadState();
    }

    private async Task SaveStateAsync()
    {
        _logger.LogInformation("Saving state");

        if (_state != null)
        {
            await _stateService.SaveStateAsync(_parameters.UserId, _parameters.SourceId, _state);
        }
    }

    private void CreateDropboxClient()
    {
        if (_dropboxClient != null)
        {
            return;
        }

        _dropboxClient = DropboxClientFactory.Create(_parameters.AuthResult, _parameters.ClientId, _httpClient);
    }

    private async Task<T> WrapApiCallAsync<T>(Func<Task<T>> apiCall, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await apiCall();
            }
            catch (RateLimitException e) when (attempt <= MaxRateLimitRetries)
            {
                // Dropbox rate limits per user and per app and says when to come back; without this the file
                // would be counted as failed and skipped until the next run
                var retryAfter = TimeSpan.FromSeconds(Math.Max(e.RetryAfter, 1));

                _logger.LogWarning(
                    "Dropbox rate limit reached, retrying in {RetryAfter} (attempt {Attempt} of {MaxAttempts})",
                    retryAfter, attempt, MaxRateLimitRetries);

                await Task.Delay(retryAfter, cancellationToken);
            }
            catch (AuthException e)
            {
                if (e.ErrorResponse == AuthError.ExpiredAccessToken.Instance)
                {
                    _logger.LogError("Access token has expired.");

                    throw new DropboxException("Access token has expired.", isAuthError: true);
                }

                _logger.LogError(e, "An auth error has occurred while calling API");

                throw new DropboxException("An auth error has occurred while calling API: " + e.Message, isAuthError: true);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error has occurred while calling API");

                throw new DropboxException("An error has occurred while calling API: " + e.Message);
            }
        }
    }

    #endregion Private Methods
}