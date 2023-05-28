using System.Runtime.CompilerServices;
using Dropbox.Api;
using Dropbox.Api.Auth;
using Dropbox.Api.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotoMap.Shared;

namespace PhotoMap.Api.Services.Services;

public class DropboxDownloadState
{
    public long UserId { get; set; }
    public int TotalFiles { get; set; }
    public int? LastProcessedFileIndex { get; set; }
    public string? LastProcessedFileId { get; set; }
    public DateTimeOffset? LastAccessTime { get; set; }
}

public interface IDownloadServiceFactory
{
    IDownloadService Create(string settingsSerialized);
}

public class DropboxDownloadServiceFactory : IDownloadServiceFactory
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    
    public DropboxDownloadServiceFactory(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }
    
    public IDownloadService Create(string settingsSerialized)
    {
        var settings = System.Text.Json.JsonSerializer.Deserialize<DropboxSettings>(settingsSerialized);

        var serviceProvider = _serviceScopeFactory.CreateScope().ServiceProvider;
        
        var logger = serviceProvider.GetRequiredService<ILogger<DropboxDownloadService>>();
        var downloadStateService = serviceProvider.GetRequiredService<IDropboxDownloadStateService>();
        var progressReporter = serviceProvider.GetRequiredService<IProgressReporter>();

        return new DropboxDownloadService(logger, downloadStateService, progressReporter, settings!);
    }
}

public class YandexDiskDownloadServiceFactory : IDownloadServiceFactory
{
    public IDownloadService Create(string settingsSerialized)
    {
        throw new NotImplementedException();
    }
}

public class DropboxDownloadService : IDownloadService
{
    private readonly ILogger<DropboxDownloadService> _logger;
    private readonly IDropboxDownloadStateService _stateService;
    private readonly IProgressReporter _progressReporter;
    private readonly DropboxSettings _settings;
    private DropboxClient _dropboxClient;
    private readonly HttpClient _httpClient;
    private DropboxDownloadState? _state;
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
    
    public async IAsyncEnumerable<DownloadedFileInfo?> DownloadAsync(
        IUserIdentifier userIdentifier,
        string apiToken,
        StopDownloadAction stoppingAction,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        CreateClient(apiToken);

        var account = await WrapApiCallAsync(_dropboxClient.Users.GetCurrentAccountAsync);

        _state = _stateService.GetState(userIdentifier.UserId);
        
        if (_state != null)
        {
            _lastProcessedFileIndex = _state.LastProcessedFileIndex;
        }
        else
        {
            _state = new DropboxDownloadState { LastAccessTime = DateTimeOffset.UtcNow, UserId = userIdentifier.UserId };
        }

        bool firstIteration = true;
        var filesMetadata = new List<Metadata>();

        var listFolderResult = await WrapApiCallAsync(() => _dropboxClient.Files.ListFolderAsync(_settings.SourceFolder, limit: (uint?)_settings.DownloadLimit));

        while (listFolderResult.HasMore || firstIteration)
        {
            if (!firstIteration)
            {
                listFolderResult = await WrapApiCallAsync(() => _dropboxClient.Files.ListFolderContinueAsync(listFolderResult.Cursor));
            }

            filesMetadata.AddRange(listFolderResult.Entries.Where(a => a is FileMetadata));

            firstIteration = false;
        }

        _state.TotalFiles = filesMetadata.Count;

        var index = _lastProcessedFileIndex > -1 ? _lastProcessedFileIndex : 0;

        for (int i = index.Value; i < filesMetadata.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested || stoppingAction.IsStopRequested)
            {
                _logger.LogInformation("Cancellation requested");
                yield break;
            }

            var fileMetadata = filesMetadata[i];

            var dropboxFile = await DownloadFileAsync(fileMetadata);
            if (dropboxFile == null)
            {
                yield break;
            }

            _state.LastProcessedFileIndex++;
            _state.LastProcessedFileId = dropboxFile.FileId;

            // TODO: revert
            // _progressReporter.Report(userIdentifier, _state.LastProcessedFileIndex, _state.TotalFiles);

            yield return dropboxFile;
        }
    }
    
    private async Task<DownloadedFileInfo?> DownloadFileAsync(Metadata metadata)
    {
        var metadataName = metadata.Name;

        try
        {
            _logger.LogInformation("Started downloading {MetadataName}", metadataName);

            var path = _settings.SourceFolder + "/" + metadataName;
            var fileMetadata = await WrapApiCallAsync(() => _dropboxClient.Files.DownloadAsync(path));
            var fileContents = await fileMetadata.GetContentAsByteArrayAsync();

            _logger.LogInformation("Finished downloading {MetadataName}", metadataName);

            var createdOn = fileMetadata.Response.ClientModified;

            return new DownloadedFileInfo(metadataName, path, createdOn, fileContents, fileMetadata.Response.Id);
        }
        catch (Exception e)
        {
            _logger.LogError("Failed downloading/saving {MetadataName}: {ErrorMessage}.", metadataName, e.Message);
        }

        return null;
    }

    private void CreateClient(string apiToken)
    {
        var config = new DropboxClientConfig("PhotoMap") { HttpClient = _httpClient };

        _dropboxClient = new DropboxClient(apiToken, config);
    }

    private void SaveState()
    {
        _logger.LogInformation("Saving state");
        _logger.LogInformation(
            "Files processed/total - {_state.LastProcessedFileIndex}/{_state.TotalFiles}");

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
}