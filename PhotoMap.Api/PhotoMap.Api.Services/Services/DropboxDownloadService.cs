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

public sealed class DropboxDownloadService : DownloadServiceBase<DropboxDownloadState>
{
    private const int MaxRateLimitRetries = 3;

    /// <summary>
    /// How many files are listed at a time. The cursor of a page is saved once the page has been processed, so
    /// this is how much of a stopped run is listed again when it resumes. Dropbox allows up to 2000, but a page
    /// that large is only finished at the end of a long run, which leaves nothing to resume from.
    /// </summary>
    private const int ListPageSize = 100;

    private readonly DropboxSettings _settings;
    private readonly HttpClient _httpClient;
    private DropboxClient? _dropboxClient;

    public DropboxDownloadService(
        ILogger<DropboxDownloadService> logger,
        IDownloadStateService<DropboxDownloadState> stateService,
        IPhotoService photoService,
        IFailedFileService failedFileService,
        IHttpClientFactory httpClientFactory,
        DropboxSettings settings,
        DownloadServiceParameters parameters)
        : base(logger, stateService, photoService, failedFileService, parameters)
    {
        _settings = settings;
        _httpClient = httpClientFactory.CreateClient("dropboxClient");
    }

    protected override int PageSize => ListPageSize;

    protected override int MaxParallelDownloads => _settings.MaxParallelDownloads;

    #region Public Methods

    public override async IAsyncEnumerable<DownloadedFile> DownloadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var state = await GetOrCreateStateAsync();

        CreateClient();

        // The cursor of a page is saved only after all of its files have been processed. After a restart the
        // unfinished page is listed again and the files saved in the meantime are skipped by their Dropbox file ID.
        var listFolderResult = await ListFolderAsync(state.Cursor, cancellationToken);

        while (true)
        {
            var filesToDownload = await GetFilesToDownloadAsync(listFolderResult);

            await foreach (var downloadedFile in DownloadPageAsync(filesToDownload, DownloadFileOrSkipAsync, cancellationToken))
            {
                yield return downloadedFile;
            }

            state.Cursor = listFolderResult.Cursor;

            Logger.LogInformation("Saving state");
            await SaveStateAsync(state);

            if (!listFolderResult.HasMore)
            {
                break;
            }

            listFolderResult = await ListFolderAsync(state.Cursor, cancellationToken);
        }
    }

    public override async Task<byte[]> DownloadFileAsync(string? externalId, string? path, CancellationToken cancellationToken)
    {
        // the file ID stays the same when the file is moved or renamed, the path doesn't
        var fileReference = externalId ?? path
            ?? throw new ArgumentException("Either the file ID or the path of the file must be given.", nameof(path));

        CreateClient();

        Logger.LogInformation("Started downloading {FileReference}", fileReference);

        using var response = await WrapApiCallAsync(() => _dropboxClient!.Files.DownloadAsync(fileReference), cancellationToken);

        await using var contentStream = await response.GetContentAsStreamAsync();
        using var memoryStream = new MemoryStream();

        await contentStream.CopyToAsync(memoryStream, cancellationToken);

        Logger.LogInformation("Finished downloading {FileReference}", fileReference);

        return memoryStream.ToArray();
    }

    public override async Task<int> GetTotalFileCountAsync()
    {
        CreateClient();

        var totalCount = 0;
        string? cursor = null;

        do
        {
            var listFolderResult = cursor == null
                ? await WrapApiCallAsync(() => _dropboxClient!.Files.ListFolderAsync(_settings.SourceFolder, limit: ListPageSize))
                : await WrapApiCallAsync(() => _dropboxClient!.Files.ListFolderContinueAsync(cursor));

            totalCount += GetSupportedFiles(listFolderResult).Count();
            cursor = listFolderResult.HasMore ? listFolderResult.Cursor : null;
        } while (cursor != null);

        return totalCount;
    }

    public override ValueTask DisposeAsync()
    {
        // the HTTP client is not disposed here, its handler is pooled and reused by IHttpClientFactory
        _dropboxClient?.Dispose();

        return ValueTask.CompletedTask;
    }

    #endregion Public Methods

    #region Protected Methods

    protected override void CreateClient()
    {
        if (_dropboxClient != null)
        {
            return;
        }

        _dropboxClient = DropboxClientFactory.Create(Parameters.AuthResult, Parameters.ClientId, _httpClient);
    }

    /// <summary>
    /// The file is downloaded by its ID, which stays the same when it is moved or renamed.
    /// </summary>
    protected override Task<DownloadedFile> RetryFileAsync(FailedFile failedFile, CancellationToken cancellationToken)
    {
        return DownloadFileWithInfoAsync(failedFile.ExternalId, failedFile.FileName, cancellationToken);
    }

    #endregion Protected Methods

    #region Private Methods

    private Task<ListFolderResult> ListFolderAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        if (cursor == null)
        {
            return WrapApiCallAsync(() => _dropboxClient!.Files.ListFolderAsync(_settings.SourceFolder, limit: ListPageSize), cancellationToken);
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
                Logger.LogWarning("Dropbox cursor has been reset, listing the folder from the start");

                return await _dropboxClient!.Files.ListFolderAsync(_settings.SourceFolder, limit: ListPageSize);
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

        var savedExternalIds = await PhotoService.GetSavedExternalIdsAsync(Parameters.UserId, Parameters.SourceId,
            files.Select(a => a.Id));

        return files.Where(a => !savedExternalIds.Contains(a.Id)).ToList();
    }

    private Task<DownloadedFile?> DownloadFileOrSkipAsync(FileMetadata fileMetadata, CancellationToken cancellationToken)
    {
        return DownloadOrSkipAsync(() => DownloadFileWithInfoAsync(fileMetadata.Id, fileMetadata.Name, cancellationToken),
            fileMetadata.Id, fileMetadata.PathDisplay, fileMetadata.Name);
    }

    /// <param name="fileId">The Dropbox file ID of the file.</param>
    /// <param name="metadataName">The name of the file, for the log.</param>
    private async Task<DownloadedFile> DownloadFileWithInfoAsync(string fileId, string metadataName, CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogInformation("Started downloading {MetadataName}", metadataName);

            // the contents are read within the wrapped call: a failure reading them skips the file like any other
            // API error, rather than ending the run
            var (fileMetadata, fileContents) = await WrapApiCallAsync(async () =>
            {
                using var response = await _dropboxClient!.Files.DownloadAsync(fileId);

                return (response.Response, await response.GetContentAsByteArrayAsync());
            }, cancellationToken);

            Logger.LogInformation("Finished downloading {MetadataName}", metadataName);

            var fileInfo = new DownloadedFileInfo(fileMetadata.Name, fileMetadata.PathDisplay, fileMetadata.ClientModified,
                fileMetadata.Id);

            return new DownloadedFile(fileInfo, fileContents);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Logger.LogError("Failed downloading/saving {MetadataName}: {ErrorMessage}.", metadataName, e.Message);
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
            catch (RateLimitException e) when (attempt <= MaxRateLimitRetries)
            {
                // Dropbox rate limits per user and per app and says when to come back; without this the file
                // would be counted as failed and skipped until the next run
                var retryAfter = TimeSpan.FromSeconds(Math.Max(e.RetryAfter, 1));

                Logger.LogWarning(
                    "Dropbox rate limit reached, retrying in {RetryAfter} (attempt {Attempt} of {MaxAttempts})",
                    retryAfter, attempt, MaxRateLimitRetries);

                await Task.Delay(retryAfter, cancellationToken);
            }
            catch (AuthException e)
            {
                if (e.ErrorResponse == AuthError.ExpiredAccessToken.Instance)
                {
                    Logger.LogError("Access token has expired.");

                    throw new DropboxException("Access token has expired.", isAuthError: true);
                }

                Logger.LogError(e, "An auth error has occurred while calling API");

                throw new DropboxException("An auth error has occurred while calling API: " + e.Message, isAuthError: true);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "An error has occurred while calling API");

                throw new DropboxException("An error has occurred while calling API: " + e.Message);
            }
        }
    }

    #endregion Private Methods
}
