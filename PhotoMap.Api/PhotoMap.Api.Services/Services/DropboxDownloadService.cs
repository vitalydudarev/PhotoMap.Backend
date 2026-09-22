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
        DropboxSettings settings,
        DownloadServiceParameters parameters)
    {
        _logger = logger;
        _stateService = stateService;
        _progressReporter = progressReporter;
        _photoService = photoService;
        _settings = settings;
        _parameters = parameters;
        _httpClient = new HttpClient();
    }

    #region Public Methods

    public async IAsyncEnumerable<DownloadedFile> DownloadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _state = await GetOrCreateStateAsync();

        CreateDropboxClient();

        // The cursor saved is always the one the current page was listed with. Files of a finished page can still
        // sit in the in-memory processing queues, so after a restart the last page is listed again and the files
        // that were saved in the meantime are skipped by their Dropbox file ID.
        var pageCursor = _state.Cursor;
        var listFolderResult = await ListFolderAsync(pageCursor);

        while (true)
        {
            foreach (var fileMetadata in listFolderResult.Entries.OfType<FileMetadata>())
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

                yield return await DownloadFileAsync(fileMetadata);
            }

            _state.Cursor = pageCursor;
            await SaveStateAsync();

            if (!listFolderResult.HasMore)
            {
                break;
            }

            pageCursor = listFolderResult.Cursor;
            listFolderResult = await ListFolderAsync(pageCursor);
        }
    }

    public async Task<int> GetTotalFileCountAsync()
    {
        CreateDropboxClient();
        
        int totalCount = 0;

        bool firstIteration = true;
        var listFolderResult = await WrapApiCallAsync(() => _dropboxClient.Files.ListFolderAsync(_settings.SourceFolder, limit: (uint?)_settings.DownloadLimit));
        
        do
        {
            if (!firstIteration)
            {
                listFolderResult = await WrapApiCallAsync(() => _dropboxClient.Files.ListFolderContinueAsync(listFolderResult.Cursor));
            }

            firstIteration = false;

            totalCount += listFolderResult.Entries.Count;
        } while (listFolderResult.HasMore);

        return totalCount;
    }

    public ValueTask DisposeAsync()
    {
        _dropboxClient?.Dispose();
        _httpClient.Dispose();

        return ValueTask.CompletedTask;
    }
    
    #endregion Public Methods

    #region Private Methods

    private Task<ListFolderResult> ListFolderAsync(string? cursor)
    {
        if (cursor == null)
        {
            return WrapApiCallAsync(() => _dropboxClient!.Files.ListFolderAsync(_settings.SourceFolder, limit: (uint?)_settings.DownloadLimit));
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
        });
    }

    private async Task<DownloadedFile> DownloadFileAsync(FileMetadata metadata)
    {
        var metadataName = metadata.Name;

        try
        {
            _logger.LogInformation("Started downloading {MetadataName}", metadataName);

            var fileMetadata = await WrapApiCallAsync(() => _dropboxClient!.Files.DownloadAsync(metadata.Id));
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
        var config = new DropboxClientConfig("PhotoMap") { HttpClient = _httpClient };

        _dropboxClient = new DropboxClient(_parameters.Token, config);
    }

    private async Task<T> WrapApiCallAsync<T>(Func<Task<T>> apiCall)
    {
        try
        {
            return await apiCall();
        }
        catch (AuthException e)
        {
            if (e.ErrorResponse == AuthError.ExpiredAccessToken.Instance)
            {
                _logger.LogError("Access token has expired.");
                
                throw new DropboxException("Access token has expired.");
            }

            _logger.LogError(e, "An auth error has occurred while calling API");
            
            throw new DropboxException("An auth error has occurred while calling API: " + e.Message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error has occurred while calling API");
            
            throw new DropboxException("An error has occurred while calling API: " + e.Message);
        }
    }

    #endregion Private Methods
}