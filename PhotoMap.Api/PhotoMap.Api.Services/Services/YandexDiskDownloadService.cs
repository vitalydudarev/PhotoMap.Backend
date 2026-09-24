using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services.Services;

public sealed class YandexDiskDownloadService : IDownloadService
{
    private const int MaxRateLimitRetries = 3;

    /// <summary>
    /// The folder is listed oldest first: the files uploaded after a run are listed after the ones it has listed,
    /// so the next run carries on from the offset the previous one has reached.
    /// </summary>
    private const string SortOrder = "created";

    private readonly ILogger<YandexDiskDownloadService> _logger;
    private readonly IYandexDiskDownloadStateService _stateService;
    private readonly IPhotoService _photoService;
    private readonly YandexDiskSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly DownloadServiceParameters _parameters;
    private ApiClient? _apiClient;
    private string? _sourceFolder;
    private YandexDiskDownloadState? _state;

    public YandexDiskDownloadService(
        ILogger<YandexDiskDownloadService> logger,
        IYandexDiskDownloadStateService stateService,
        IPhotoService photoService,
        IHttpClientFactory httpClientFactory,
        YandexDiskSettings settings,
        DownloadServiceParameters parameters)
    {
        _logger = logger;
        _stateService = stateService;
        _photoService = photoService;
        _settings = settings;
        _parameters = parameters;
        _httpClient = httpClientFactory.CreateClient("yandexDiskClient");
    }

    #region Public Methods

    public async IAsyncEnumerable<DownloadedFile> DownloadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _state = await GetOrCreateStateAsync();

        CreateApiClient();

        var sourceFolder = await GetSourceFolderAsync(cancellationToken);

        // The offset of a page is saved only after all of its files have been processed. After a restart the
        // unfinished page is listed again and the files saved in the meantime are skipped by their resource ID.
        // Files deleted from the folder before the saved offset shift the ones after it back by as many, a resumed
        // run misses that many files at the offset; deleting the data of the source lists the folder from the start.
        while (true)
        {
            var page = await ListFolderAsync(sourceFolder, _state.Offset, cancellationToken);
            var items = page.Items ?? [];

            var pageFiles = new List<DownloadedFile>();

            var filesToDownload = await GetFilesToDownloadAsync(items);

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

            _state.Offset += items.Length;
            await SaveStateAsync();

            if (items.Length < GetPageSize() || _state.Offset >= page.Total)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Yandex.Disk downloads files by path only, the resource ID of a file cannot be used to download it.
    /// </summary>
    public async Task<byte[]> DownloadFileAsync(string? externalId, string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Yandex.Disk files are downloaded by path, the path is missing.", nameof(path));
        }

        CreateApiClient();

        _logger.LogInformation("Started downloading {Path}", path);

        var fileContents = await WrapApiCallAsync(() => _apiClient!.DownloadFileAsync(path, cancellationToken), cancellationToken);

        _logger.LogInformation("Finished downloading {Path}", path);

        return fileContents;
    }

    public async Task<int> GetTotalFileCountAsync()
    {
        CreateApiClient();

        var sourceFolder = await GetSourceFolderAsync(CancellationToken.None);

        var totalCount = 0;
        var offset = 0;

        while (true)
        {
            var page = await ListFolderAsync(sourceFolder, offset);
            var items = page.Items ?? [];

            totalCount += GetSupportedFiles(items).Count();
            offset += items.Length;

            if (items.Length < GetPageSize() || offset >= page.Total)
            {
                break;
            }
        }

        return totalCount;
    }

    public ValueTask DisposeAsync()
    {
        // the HTTP client is not disposed here, its handler is pooled and reused by IHttpClientFactory
        return ValueTask.CompletedTask;
    }

    #endregion Public Methods

    #region Private Methods

    private async Task<ResourceList> ListFolderAsync(string sourceFolder, int offset, CancellationToken cancellationToken = default)
    {
        var resource = await WrapApiCallAsync(
            () => _apiClient!.GetResourceAsync(sourceFolder, cancellationToken, offset, GetPageSize(), SortOrder),
            cancellationToken);

        if (resource.Embedded == null)
        {
            throw new YandexDiskException($"{sourceFolder} is not a folder.");
        }

        return resource.Embedded;
    }

    private async Task<string> GetSourceFolderAsync(CancellationToken cancellationToken)
    {
        if (_sourceFolder != null)
        {
            return _sourceFolder;
        }

        if (_settings.UsePhotoStreamFolder)
        {
            // the name of the folder depends on the language of the user, e.g. "disk:/Camera Uploads/"
            var disk = await WrapApiCallAsync(() => _apiClient!.GetDiskAsync(cancellationToken), cancellationToken);

            _sourceFolder = disk.SystemFolders?.Photostream
                ?? throw new YandexDiskException("The user has no Photostream folder.");
        }
        else
        {
            _sourceFolder = string.IsNullOrEmpty(_settings.SourceFolder)
                ? throw new YandexDiskException("The source folder is not set.")
                : _settings.SourceFolder;
        }

        return _sourceFolder;
    }

    private static IEnumerable<Resource> GetSupportedFiles(IEnumerable<Resource> items)
    {
        return items.Where(a => a.Type == "file" && SupportedImageFormats.IsSupported(a.Name));
    }

    /// <summary>
    /// The files of the page still to download. Saved files are looked up before the downloads start, in one
    /// query: the database context of the run is not thread safe, so the check cannot run alongside them.
    /// </summary>
    private async Task<IReadOnlyCollection<Resource>> GetFilesToDownloadAsync(IEnumerable<Resource> items)
    {
        var files = GetSupportedFiles(items).ToList();

        var savedExternalIds = await _photoService.GetSavedExternalIdsAsync(_parameters.UserId, _parameters.SourceId,
            files.Select(a => a.ResourceId));

        return files.Where(a => !savedExternalIds.Contains(a.ResourceId)).ToList();
    }

    /// <summary>
    /// Downloads the files of one page, several at a time. They are handed over in the order they finish
    /// downloading, which nothing depends on: the offset of the page is saved once all of them have been
    /// processed, however they were ordered.
    /// </summary>
    private IAsyncEnumerable<DownloadedFile> DownloadPageAsync(
        IReadOnlyCollection<Resource> files,
        CancellationToken cancellationToken)
    {
        return ParallelDownloads.RunAsync(files, GetMaxParallelDownloads(), DownloadFileOrSkipAsync, cancellationToken);
    }

    private async Task<DownloadedFile?> DownloadFileOrSkipAsync(Resource resource, CancellationToken cancellationToken)
    {
        try
        {
            return await DownloadFileAsync(resource, cancellationToken);
        }
        catch (YandexDiskException e) when (!e.IsAuthError)
        {
            // skip the file, the error has been logged
            _parameters.Progress.FileFailed();

            return null;
        }
    }

    private int GetPageSize()
    {
        return Math.Max(_settings.DownloadLimit, 1);
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

    private async Task<DownloadedFile> DownloadFileAsync(Resource resource, CancellationToken cancellationToken)
    {
        var resourceName = resource.Name;

        try
        {
            _logger.LogInformation("Started downloading {ResourceName}", resourceName);

            var fileContents = await WrapApiCallAsync(() => _apiClient!.DownloadFileAsync(resource.Path, cancellationToken),
                cancellationToken);

            _logger.LogInformation("Finished downloading {ResourceName}", resourceName);

            // the time the photo was taken, when Yandex.Disk has read it from EXIF, otherwise the upload time
            var createdOn = resource.Exif?.DateTime ?? resource.PhotosliceTime ?? resource.Created;

            var fileInfo = new DownloadedFileInfo(resourceName, resource.Path, createdOn, resource.ResourceId);

            return new DownloadedFile(fileInfo, fileContents);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError("Failed downloading/saving {ResourceName}: {ErrorMessage}.", resourceName, e.Message);
            throw;
        }
    }

    private async Task<YandexDiskDownloadState> GetOrCreateStateAsync()
    {
        var state = await _stateService.GetStateAsync(_parameters.UserId, _parameters.SourceId);

        return state ?? new YandexDiskDownloadState();
    }

    private async Task SaveStateAsync()
    {
        _logger.LogInformation("Saving state");

        if (_state != null)
        {
            await _stateService.SaveStateAsync(_parameters.UserId, _parameters.SourceId, _state);
        }
    }

    private void CreateApiClient()
    {
        if (_apiClient != null)
        {
            return;
        }

        // Yandex.Disk tokens are not refreshed, the user authorizes again when the token has expired
        _apiClient = new ApiClient(_parameters.AuthResult.Token, _httpClient);
    }

    private async Task<T> WrapApiCallAsync<T>(Func<Task<T>> apiCall, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await apiCall();
            }
            catch (ApiException e) when (e.StatusCode == HttpStatusCode.TooManyRequests && attempt <= MaxRateLimitRetries)
            {
                // without this the file would be counted as failed and skipped until the next run
                var retryAfter = e.RetryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));

                _logger.LogWarning(
                    "Yandex.Disk rate limit reached, retrying in {RetryAfter} (attempt {Attempt} of {MaxAttempts})",
                    retryAfter, attempt, MaxRateLimitRetries);

                await Task.Delay(retryAfter, cancellationToken);
            }
            catch (ApiException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogError(e, "An auth error has occurred while calling API");

                throw new YandexDiskException("An auth error has occurred while calling API: " + e.Message, isAuthError: true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error has occurred while calling API");

                throw new YandexDiskException("An error has occurred while calling API: " + e.Message);
            }
        }
    }

    #endregion Private Methods
}
