using Dropbox.Api;
using Dropbox.Api.Files;
using Dropbox.Api.Stone;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The files of the folder of a Dropbox its photos are processed from.
/// </summary>
public sealed class DropboxSourceFiles : ISourceFiles
{
    private const int ListPageSize = 2000;

    /// <summary>
    /// How long the download of a file waits to start, for the headers of the response, before the attempt fails.
    /// Reading the contents takes as long as it does.
    /// </summary>
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How many times the download of a file is tried again when it fails. The client the files are downloaded with
    /// does not retry by itself, see <see cref="DropboxClientFactory.Create"/>.
    /// </summary>
    public const int DownloadMaxRetries = 2;

    /// <summary>
    /// The sizes of the thumbnail, the largest first: a photo Dropbox cannot make one size of may make a smaller one.
    /// </summary>
    private static readonly ThumbnailSize[] PreviewSizes =
    [
        ThumbnailSize.W2048h1536.Instance,
        ThumbnailSize.W1024h768.Instance,
        ThumbnailSize.W960h640.Instance,
        ThumbnailSize.W640h480.Instance,
        ThumbnailSize.W480h320.Instance,
        ThumbnailSize.W256h256.Instance
    ];

    private readonly DropboxClient _dropboxClient;
    private readonly DropboxClient _downloadClient;
    private readonly DropboxSettings _settings;
    private readonly ILogger _logger;
    private readonly Func<int, TimeSpan> _retryDelay;

    /// <param name="dropboxClient">Lists the files and makes their previews.</param>
    /// <param name="downloadClient">Downloads the files, with no retries of its own.</param>
    /// <param name="retryDelay">How long to wait before the retry of the attempt given, 2 then 4 seconds when not
    /// given. A rate limit reached waits as long as Dropbox asks for instead.</param>
    public DropboxSourceFiles(DropboxClient dropboxClient, DropboxClient downloadClient, DropboxSettings settings, ILogger logger,
        Func<int, TimeSpan>? retryDelay = null)
    {
        _dropboxClient = dropboxClient;
        _downloadClient = downloadClient;
        _settings = settings;
        _logger = logger;
        _retryDelay = retryDelay ?? (attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
    }

    public int MaxParallelDownloads => Math.Max(_settings.MaxParallelFileDownloads, 1);

    public async Task<IReadOnlyList<SourceFile>> ListAsync(SourceDownloadContent content, string? folder, bool includeSubfolders,
        CancellationToken cancellationToken)
    {
        if (folder != null)
        {
            throw new NotSupportedException("The folders of Dropbox cannot be chosen, its files are downloaded from its source folder.");
        }

        var files = new List<SourceFile>();
        var listFolderResult = await _dropboxClient.Files.ListFolderAsync(_settings.SourceFolder, recursive: includeSubfolders,
            limit: ListPageSize);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var fileMetadata in listFolderResult.Entries.OfType<FileMetadata>())
            {
                var fileContent = DownloadableFormats.GetContent(fileMetadata.Name);
                if (fileContent != null && content.HasFlag(fileContent.Value))
                {
                    // the file ID stays the same when the file is moved or renamed, the path doesn't
                    files.Add(new SourceFile(fileMetadata.Name, fileMetadata.Id, (long)fileMetadata.Size, fileContent.Value));
                }
            }

            if (!listFolderResult.HasMore)
            {
                break;
            }

            listFolderResult = await _dropboxClient.Files.ListFolderContinueAsync(listFolderResult.Cursor);
        }

        return files;
    }

    public Task<IReadOnlyList<SourceFile>> ListOtherUploadsAsync(SourceDownloadContent content, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("The other uploads of Dropbox are not imported, there are none to download.");
    }

    /// <summary>
    /// The download waits <see cref="DownloadTimeout"/> to start, and is tried again up to
    /// <see cref="DownloadMaxRetries"/> times.
    /// </summary>
    public async Task CopyToAsync(SourceFile file, Stream destination, CancellationToken cancellationToken)
    {
        using var response = await OpenDownloadAsync(file.Path, cancellationToken);
        await using var contentStream = await response.GetContentAsStreamAsync();

        await contentStream.CopyToAsync(destination, cancellationToken);
    }

    /// <summary>
    /// Starts downloading the file, again up to <see cref="DownloadMaxRetries"/> times when it fails. A token Dropbox
    /// does not accept fails at once, as do a file it cannot download and a stopped download: none of them would go
    /// any differently on a retry.
    /// </summary>
    private async Task<IDownloadResponse<FileMetadata>> OpenDownloadAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await StartDownloadAsync(path, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (e is not AuthException and not ApiException<DownloadError> && attempt <= DownloadMaxRetries)
            {
                var delay = e is RateLimitException rateLimit
                    ? TimeSpan.FromSeconds(Math.Max(rateLimit.RetryAfter, 1))
                    : _retryDelay(attempt);

                _logger.LogWarning(e, "Downloading {Path} failed: {ErrorMessage}, retrying in {RetryDelay} (attempt {Attempt} of {MaxAttempts})",
                    path, e.Message, delay, attempt, DownloadMaxRetries);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// The Dropbox SDK cannot cancel a download, so it is waited for <see cref="DownloadTimeout"/> at most. A download
    /// given up on is disposed if it starts after all.
    /// </summary>
    /// <exception cref="TimeoutException">The download has not started within <see cref="DownloadTimeout"/>.</exception>
    private async Task<IDownloadResponse<FileMetadata>> StartDownloadAsync(string path, CancellationToken cancellationToken)
    {
        var download = _downloadClient.Files.DownloadAsync(path);

        try
        {
            return await download.WaitAsync(DownloadTimeout, cancellationToken);
        }
        catch (Exception e) when (e is TimeoutException or OperationCanceledException)
        {
            _ = download.ContinueWith(a =>
            {
                if (a.IsCompletedSuccessfully)
                {
                    a.Result.Dispose();
                }
                else
                {
                    // observed, so that the failure of a download given up on is not reported as unobserved
                    _ = a.Exception;
                }
            }, TaskScheduler.Default);

            if (e is TimeoutException)
            {
                throw new TimeoutException($"The download of {path} has not started within {DownloadTimeout.TotalSeconds:0} seconds.", e);
            }

            throw;
        }
    }

    /// <summary>
    /// The largest thumbnail Dropbox makes of the file. Dropbox makes thumbnails of photos only, of the formats it
    /// supports and up to 20 MB: there is no preview of any other file.
    /// </summary>
    public async Task<SourcePreview?> DownloadPreviewAsync(string reference, CancellationToken cancellationToken)
    {
        foreach (var size in PreviewSizes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await _dropboxClient.Files.GetThumbnailAsync(reference, ThumbnailFormat.Jpeg.Instance, size);
                var contents = await response.GetContentAsByteArrayAsync();

                return contents.Length == 0 ? null : new SourcePreview(contents, "image/jpeg");
            }
            catch (ApiException<ThumbnailError> e) when (e.ErrorResponse.IsConversionError)
            {
                // a smaller thumbnail may be made of it
            }
            catch (ApiException<ThumbnailError> e) when (e.ErrorResponse.IsUnsupportedExtension ||
                                                         e.ErrorResponse.IsUnsupportedImage ||
                                                         e.ErrorResponse.IsEncryptedContent)
            {
                return null;
            }
        }

        return null;
    }

    public bool IsAuthError(Exception exception)
    {
        return exception is AuthException;
    }

    public void Dispose()
    {
        _dropboxClient.Dispose();
        _downloadClient.Dispose();
    }
}
