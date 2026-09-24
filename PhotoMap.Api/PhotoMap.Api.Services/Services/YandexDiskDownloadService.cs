using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services.Services;

public sealed class YandexDiskDownloadService : DownloadServiceBase<YandexDiskDownloadState>
{
    private const int MaxRateLimitRetries = 3;

    /// <summary>
    /// The folder is listed oldest first: the files uploaded after a run are listed after the ones it has listed,
    /// so the next run carries on from the offset the previous one has reached.
    /// </summary>
    private const string SortOrder = "created";

    private readonly YandexDiskSettings _settings;
    private readonly HttpClient _httpClient;
    private ApiClient? _apiClient;
    private string? _sourceFolder;

    public YandexDiskDownloadService(
        ILogger<YandexDiskDownloadService> logger,
        IDownloadStateService<YandexDiskDownloadState> stateService,
        IPhotoService photoService,
        IFailedFileService failedFileService,
        IHttpClientFactory httpClientFactory,
        YandexDiskSettings settings,
        DownloadServiceParameters parameters)
        : base(logger, stateService, photoService, failedFileService, parameters)
    {
        _settings = settings;
        _httpClient = httpClientFactory.CreateClient("yandexDiskClient");
    }

    protected override int PageSize => Math.Max(_settings.DownloadLimit, 1);

    protected override int MaxParallelDownloads => _settings.MaxParallelDownloads;

    #region Public Methods

    public override async IAsyncEnumerable<DownloadedFile> DownloadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var state = await GetOrCreateStateAsync();

        CreateClient();

        var sourceFolder = await GetSourceFolderAsync(cancellationToken);

        // A page is processed SaveStateEvery files at a time, the offset saved after each of them. The files of a
        // chunk finish in any order, so the offset is only saved once all of them have been processed: every file
        // before it is done. After a restart the unfinished chunk is listed again and the files saved in the
        // meantime are skipped by their resource ID. Files deleted from the folder before the saved offset shift the
        // ones after it back by as many, a resumed run misses that many files at the offset; deleting the data of
        // the source lists the folder from the start.
        while (true)
        {
            var page = await ListFolderAsync(sourceFolder, state.Offset, cancellationToken);
            var items = page.Items ?? [];

            foreach (var chunk in items.Chunk(Math.Max(_settings.SaveStateEvery, 1)))
            {
                var filesToDownload = await GetFilesToDownloadAsync(chunk);

                await foreach (var downloadedFile in DownloadPageAsync(filesToDownload, DownloadFileOrSkipAsync, cancellationToken))
                {
                    yield return downloadedFile;
                }

                state.Offset += chunk.Length;

                Logger.LogInformation("Saving state, offset {Offset}", state.Offset);
                await SaveStateAsync(state);
            }

            if (items.Length < PageSize || state.Offset >= page.Total)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Yandex.Disk downloads files by path only, the resource ID of a file cannot be used to download it.
    /// </summary>
    public override async Task<byte[]> DownloadFileAsync(string? externalId, string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Yandex.Disk files are downloaded by path, the path is missing.", nameof(path));
        }

        CreateClient();

        Logger.LogInformation("Started downloading {Path}", path);

        var fileContents = await WrapApiCallAsync(() => _apiClient!.DownloadFileAsync(path, cancellationToken), cancellationToken);

        Logger.LogInformation("Finished downloading {Path}", path);

        return fileContents;
    }

    public override async Task<int> GetTotalFileCountAsync()
    {
        CreateClient();

        var sourceFolder = await GetSourceFolderAsync(CancellationToken.None);

        var totalCount = 0;
        var offset = 0;

        while (true)
        {
            var page = await ListFolderAsync(sourceFolder, offset);
            var items = page.Items ?? [];

            totalCount += GetSupportedFiles(items).Count();
            offset += items.Length;

            if (items.Length < PageSize || offset >= page.Total)
            {
                break;
            }
        }

        return totalCount;
    }

    #endregion Public Methods

    #region Protected Methods

    protected override void CreateClient()
    {
        if (_apiClient != null)
        {
            return;
        }

        // Yandex.Disk tokens are not refreshed, the user authorizes again when the token has expired
        _apiClient = new ApiClient(Parameters.AuthResult.Token, _httpClient);
    }

    /// <summary>
    /// The file is looked up by its path again, for the details the listing gave.
    /// </summary>
    protected override async Task<DownloadedFile> RetryFileAsync(FailedFile failedFile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(failedFile.Path))
        {
            throw new YandexDiskException("Yandex.Disk files are downloaded by path, the path is missing.");
        }

        var resource = await WrapApiCallAsync(
            () => _apiClient!.GetResourceAsync(failedFile.Path, cancellationToken, limit: 1), cancellationToken);

        return await DownloadFileAsync(resource, cancellationToken);
    }

    #endregion Protected Methods

    #region Private Methods

    private async Task<ResourceList> ListFolderAsync(string sourceFolder, int offset, CancellationToken cancellationToken = default)
    {
        var resource = await WrapApiCallAsync(
            () => _apiClient!.GetResourceAsync(sourceFolder, cancellationToken, offset, PageSize, SortOrder),
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

        var savedExternalIds = await PhotoService.GetSavedExternalIdsAsync(Parameters.UserId, Parameters.SourceId,
            files.Select(a => a.ResourceId));

        return files.Where(a => !savedExternalIds.Contains(a.ResourceId)).ToList();
    }

    private Task<DownloadedFile?> DownloadFileOrSkipAsync(Resource resource, CancellationToken cancellationToken)
    {
        return DownloadOrSkipAsync(() => DownloadFileAsync(resource, cancellationToken), resource.ResourceId,
            resource.Path, resource.Name);
    }

    private async Task<DownloadedFile> DownloadFileAsync(Resource resource, CancellationToken cancellationToken)
    {
        var resourceName = resource.Name;

        try
        {
            Logger.LogInformation("Started downloading {ResourceName}", resourceName);

            var fileContents = await WrapApiCallAsync(() => _apiClient!.DownloadFileAsync(resource.Path, cancellationToken),
                cancellationToken);

            Logger.LogInformation("Finished downloading {ResourceName}", resourceName);

            // the time the photo was taken, when Yandex.Disk has read it from EXIF, otherwise the upload time
            var createdOn = resource.Exif?.DateTime ?? resource.PhotosliceTime ?? resource.Created;

            var fileInfo = new DownloadedFileInfo(resourceName, resource.Path, createdOn, resource.ResourceId);

            return new DownloadedFile(fileInfo, fileContents);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Logger.LogError("Failed downloading/saving {ResourceName}: {ErrorMessage}.", resourceName, e.Message);
            throw;
        }
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

                Logger.LogWarning(
                    "Yandex.Disk rate limit reached, retrying in {RetryAfter} (attempt {Attempt} of {MaxAttempts})",
                    retryAfter, attempt, MaxRateLimitRetries);

                await Task.Delay(retryAfter, cancellationToken);
            }
            catch (ApiException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
            {
                Logger.LogError(e, "An auth error has occurred while calling API");

                throw new YandexDiskException("An auth error has occurred while calling API: " + e.Message, isAuthError: true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "An error has occurred while calling API");

                throw new YandexDiskException("An error has occurred while calling API: " + e.Message);
            }
        }
    }

    #endregion Private Methods
}
