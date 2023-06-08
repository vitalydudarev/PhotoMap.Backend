using System.Runtime.CompilerServices;
using Dropbox.Api;
using Dropbox.Api.Auth;
using Dropbox.Api.Files;
using Microsoft.Extensions.Logging;

namespace PhotoMap.Api.Services.Services;

public sealed class DropboxDownloadService : IDownloadService
{
    private readonly ILogger<DropboxDownloadService> _logger;
    private readonly IDropboxDownloadStateService _stateService;
    private readonly IProgressReporter _progressReporter;
    private readonly DropboxSettings _settings;
    private DropboxClient _dropboxClient;
    private readonly HttpClient _httpClient;
    private DropboxDownloadState _state;
    private int? _lastProcessedFileIndex;

    public DropboxDownloadService(
        ILogger<DropboxDownloadService> logger,
        IDropboxDownloadStateService stateService,
        IProgressReporter progressReporter,
        DropboxSettings settings)
    {
        _logger = logger;
        _stateService = stateService;
        _progressReporter = progressReporter;
        _settings = settings;
        _httpClient = new HttpClient();
    }

    #region Public Methods

    public async IAsyncEnumerable<DownloadedFileInfo> DownloadAsync(
        long userId,
        string token,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        CreateDropboxClient(token);

        LoadOrCreateState(userId);

        var filesMetadata = await GetFileListAsync();

        _state.TotalFiles = filesMetadata.Count;

        await foreach (var downloadedFileInfo in DownloadFilesAsync(filesMetadata, cancellationToken)) yield return downloadedFileInfo;
    }

    public void Dispose()
    {
        SaveState();
        
        _dropboxClient.Dispose();
        _httpClient.Dispose();
    }
    
    #endregion Public Methods

    #region Private Methods

    private async IAsyncEnumerable<DownloadedFileInfo> DownloadFilesAsync(
        List<Metadata> filesMetadata,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var index = _lastProcessedFileIndex ?? 0;

        for (int i = index; i < filesMetadata.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Cancellation requested");
                yield break;
            }

            var fileMetadata = filesMetadata[i];

            var downloadedFileInfo = await DownloadFileAsync(fileMetadata);

            _state.LastProcessedFileIndex++;
            _state.LastProcessedFileId = downloadedFileInfo.FileId;

            // TODO: revert
            // _progressReporter.Report(userIdentifier, _state.LastProcessedFileIndex, _state.TotalFiles);

            yield return downloadedFileInfo;
        }
    }
    
    private async Task<List<Metadata>> GetFileListAsync()
    {
        var filesMetadata = new List<Metadata>();

        bool firstIteration = true;
        var listFolderResult = await WrapApiCallAsync(() => _dropboxClient.Files.ListFolderAsync(_settings.SourceFolder, limit: (uint?)_settings.DownloadLimit));
        
        do
        {
            if (!firstIteration)
            {
                listFolderResult = await WrapApiCallAsync(() => _dropboxClient.Files.ListFolderContinueAsync(listFolderResult.Cursor));
            }

            firstIteration = false;
            
            filesMetadata.AddRange(listFolderResult.Entries.Where(a => a is FileMetadata));
        } while (listFolderResult.HasMore);

        return filesMetadata;
    }

    private async Task<DownloadedFileInfo> DownloadFileAsync(Metadata metadata)
    {
        var metadataName = metadata.Name;

        try
        {
            _logger.LogInformation("Started downloading {MetadataName}", metadataName);

            var fileMetadata = await WrapApiCallAsync(() => _dropboxClient.Files.DownloadAsync(metadata.PathDisplay));
            var fileContents = await fileMetadata.GetContentAsByteArrayAsync();

            _logger.LogInformation("Finished downloading {MetadataName}", metadataName);

            var createdOn = fileMetadata.Response.ClientModified;

            return new DownloadedFileInfo(metadataName, metadata.PathDisplay, createdOn, fileContents, fileMetadata.Response.Id);
        }
        catch (Exception e)
        {
            _logger.LogError("Failed downloading/saving {MetadataName}: {ErrorMessage}.", metadataName, e.Message);
            throw;
        }
    }
    
    private void LoadOrCreateState(long userId)
    {
        var state = _stateService.GetState(userId);
        
        if (state != null)
        {
            _lastProcessedFileIndex = state.LastProcessedFileIndex;
            _state = state;
        }
        else
        {
            _state = new DropboxDownloadState { LastAccessTime = DateTimeOffset.UtcNow, UserId = userId };
        }
    }

    private void CreateDropboxClient(string token)
    {
        var config = new DropboxClientConfig("PhotoMap") { HttpClient = _httpClient };

        _dropboxClient = new DropboxClient(token, config);
    }

    private void SaveState()
    {
        _logger.LogInformation("Saving state");
        _logger.LogInformation("Files processed/total - {LastProcessedFileIndex}/{TotalFiles}", _state.LastProcessedFileIndex, _state.TotalFiles);

        _stateService.SaveState(_state);
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