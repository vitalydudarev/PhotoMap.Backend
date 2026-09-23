using System.Runtime.CompilerServices;
using Dropbox.Api;
using Dropbox.Api.Auth;
using Dropbox.Api.Files;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Shared.Models;
using DropboxException = PhotoMap.Api.Services.Exceptions.DropboxException;

namespace PhotoMap.Api.Services.Services;

public sealed class DropboxDownloadService : IDownloadService
{
    private const int MaxRateLimitRetries = 3;

    private readonly ILogger<DropboxDownloadService> _logger;
    private readonly IDropboxDownloadStateService _stateService;
    private readonly IProgressReporter _progressReporter;
    private readonly IPhotoService _photoService;
    private readonly DropboxSettings _settings;
    private DropboxClient? _dropboxClient;
    private readonly HttpClient _httpClient;
    private DropboxDownloadState? _state;
    private readonly DownloadServiceParameters _parameters;

    public DropboxDownloadService(
        ILogger<DropboxDownloadService> logger,
        IDropboxDownloadStateService stateService,
        IProgressReporter progressReporter,
        IPhotoService photoService,
        IHttpClientFactory httpClientFactory,
        DropboxSettings settings,
        DownloadServiceParameters parameters)
    {
        _logger = logger;
        _stateService = stateService;
        _progressReporter = progressReporter;
        _photoService = photoService;
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

            foreach (var fileMetadata in GetSupportedFiles(listFolderResult))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Cancellation requested");
                    yield break;
                }

                if (await _photoService.ExistsAsync(_parameters.UserId, _parameters.SourceId, fileMetadata.Id))
                {
                    continue;
                }

                DownloadedFile downloadedFile;

                try
                {
                    downloadedFile = await DownloadFileAsync(fileMetadata, cancellationToken);
                }
                catch (DropboxException e) when (!e.IsAuthError)
                {
                    // skip the file, the error has been logged
                    _parameters.Progress.FileFailed();
                    continue;
                }

                pageFiles.Add(downloadedFile);

                yield return downloadedFile;
            }

            if (!await WaitUntilProcessedAsync(pageFiles, cancellationToken))
            {
                _logger.LogInformation("Cancellation requested");
                yield break;
            }

            _state.Cursor = listFolderResult.Cursor;
            await SaveStateAsync();

            if (!listFolderResult.HasMore)
            {
                break;
            }

            listFolderResult = await ListFolderAsync(_state.Cursor, cancellationToken);
        }
    }

    public async Task<byte[]> DownloadFileAsync(string fileReference, CancellationToken cancellationToken)
    {
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
                ? await WrapApiCallAsync(() => _dropboxClient!.Files.ListFolderAsync(_settings.SourceFolder, limit: (uint?)_settings.DownloadLimit))
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
            return WrapApiCallAsync(() => _dropboxClient!.Files.ListFolderAsync(_settings.SourceFolder, limit: (uint?)_settings.DownloadLimit), cancellationToken);
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

                return await _dropboxClient!.Files.ListFolderAsync(_settings.SourceFolder, limit: (uint?)_settings.DownloadLimit);
            }
        }, cancellationToken);
    }

    private static IEnumerable<FileMetadata> GetSupportedFiles(ListFolderResult listFolderResult)
    {
        return listFolderResult.Entries.OfType<FileMetadata>().Where(a => SupportedImageFormats.IsSupported(a.Name));
    }

    private async Task<bool> WaitUntilProcessedAsync(List<DownloadedFile> files, CancellationToken cancellationToken)
    {
        try
        {
            var results = await Task.WhenAll(files.Select(a => a.Processed.Task)).WaitAsync(cancellationToken);

            var failedCount = results.Count(a => !a);
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

    private async Task<DownloadedFile> DownloadFileAsync(FileMetadata metadata, CancellationToken cancellationToken)
    {
        var metadataName = metadata.Name;

        try
        {
            _logger.LogInformation("Started downloading {MetadataName}", metadataName);

            var fileMetadata = await WrapApiCallAsync(() => _dropboxClient!.Files.DownloadAsync(metadata.Id), cancellationToken);
            var fileContents = await fileMetadata.GetContentAsByteArrayAsync();

            _logger.LogInformation("Finished downloading {MetadataName}", metadataName);

            var createdOn = fileMetadata.Response.ClientModified;

            var fileInfo = new DownloadedFileInfo(metadataName, metadata.PathDisplay, createdOn, fileMetadata.Response.Id);

            return new DownloadedFile(fileInfo, fileContents);
        }
        catch (Exception e)
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