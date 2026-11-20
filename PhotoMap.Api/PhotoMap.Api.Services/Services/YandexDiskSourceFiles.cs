using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The files of the folder of a Yandex.Disk its photos are processed from, the Photostream folder unless the source
/// is set to another one.
/// </summary>
public sealed class YandexDiskSourceFiles : ISourceFiles
{
    /// <summary>
    /// How long the download of a file waits to start, for its download URL and the headers of the response, before
    /// the attempt fails. Reading the contents takes as long as it does.
    /// </summary>
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How many times the download of a file is tried again when it fails.
    /// </summary>
    public const int DownloadMaxRetries = 2;

    /// <summary>
    /// The size of the preview a photo is viewed as, 1280 pixels on its longer side.
    /// </summary>
    public const string ViewingPreviewSizeName = "XXXL";

    private readonly ApiClient _apiClient;
    private readonly YandexDiskSettings _settings;
    private readonly ILogger _logger;

    public YandexDiskSourceFiles(ApiClient apiClient, YandexDiskSettings settings, ILogger logger)
    {
        _apiClient = apiClient;
        _settings = settings;
        _logger = logger;
    }

    public int MaxParallelDownloads => Math.Max(_settings.MaxParallelFileDownloads, 1);

    /// <summary>
    /// The folders within the folder are listed one after another, each as it is found, a folder before the ones
    /// within it.
    /// </summary>
    public async Task<IReadOnlyList<SourceFile>> ListAsync(SourceDownloadContent content, string? folder, bool includeSubfolders,
        CancellationToken cancellationToken)
    {
        var sourceFolder = folder
            ?? await YandexDiskDownloadService.GetSourceFolderAsync(_apiClient, _settings, _logger, cancellationToken);

        var files = new List<SourceFile>();
        var folders = new Queue<string>([sourceFolder]);

        while (folders.TryDequeue(out var folderToList))
        {
            await foreach (var item in YandexDiskFolders.ListFolderAsync(_apiClient, _logger, folderToList, cancellationToken))
            {
                if (item.Type == "dir")
                {
                    if (includeSubfolders)
                    {
                        folders.Enqueue(item.Path);
                    }

                    continue;
                }

                var fileContent = item.Type == "file" ? DownloadableFormats.GetContent(item.Name, item.MediaType) : null;
                if (fileContent != null && content.HasFlag(fileContent.Value))
                {
                    files.Add(new SourceFile(item.Name, item.Path, item.Size, fileContent.Value));
                }
            }
        }

        return files;
    }

    /// <summary>
    /// The photos <see cref="YandexDiskOtherUploads"/> lists, every photo of the disk but those right in the folder
    /// the photos are processed from, and the videos it lists the same way when asked for.
    /// </summary>
    public async Task<IReadOnlyList<SourceFile>> ListOtherUploadsAsync(SourceDownloadContent content,
        CancellationToken cancellationToken)
    {
        var sourceFolder = await YandexDiskDownloadService.GetSourceFolderAsync(_apiClient, _settings, _logger, cancellationToken);
        var otherUploads = new YandexDiskOtherUploads(_apiClient, _logger);

        // the disk is walked once, for the photos and the videos alike
        var diskFiles = await otherUploads.ListFilesAsync(sourceFolder, cancellationToken);
        var files = new List<SourceFile>();

        if (content.HasFlag(SourceDownloadContent.Photos))
        {
            files.AddRange(diskFiles.Where(YandexDiskOtherUploads.IsPhoto)
                .Select(a => new SourceFile(a.Name, a.Path, a.Size, SourceDownloadContent.Photos)));
        }

        if (content.HasFlag(SourceDownloadContent.Videos))
        {
            files.AddRange(diskFiles.Where(YandexDiskOtherUploads.IsVideo)
                .Select(a => new SourceFile(a.Name, a.Path, a.Size, SourceDownloadContent.Videos)));
        }

        return files;
    }

    /// <summary>
    /// The download waits <see cref="DownloadTimeout"/> to start, given the API client the factory creates, and is
    /// tried again up to <see cref="DownloadMaxRetries"/> times.
    /// </summary>
    public async Task CopyToAsync(SourceFile file, Stream destination, CancellationToken cancellationToken)
    {
        using var response = await YandexDiskApiCalls.WrapAsync(_logger,
            () => _apiClient.OpenDownloadAsync(file.Path, null, cancellationToken), cancellationToken,
            maxRetries: DownloadMaxRetries);

        await response.Content.CopyToAsync(destination, cancellationToken);
    }

    /// <summary>
    /// The largest preview Yandex.Disk has made of the file: XXXL, or the largest smaller one when it has none.
    /// </summary>
    public async Task<SourcePreview?> DownloadPreviewAsync(string reference, CancellationToken cancellationToken)
    {
        var resource = await GetResourceAsync(reference, cancellationToken);

        return await DownloadByUrlAsync(YandexDiskVideoSource.GetPreviewUrl(resource) ?? resource.Preview, cancellationToken);
    }

    /// <summary>
    /// The XXXL preview, the largest Yandex.Disk makes; the smaller ones are too small to fill a screen.
    /// </summary>
    public async Task<SourcePreviewStream?> OpenViewingPreviewAsync(string reference, CancellationToken cancellationToken)
    {
        var resource = await GetResourceAsync(reference, cancellationToken);
        var previewUrl = resource.Sizes
            .FirstOrDefault(a => a.Name == ViewingPreviewSizeName && !string.IsNullOrEmpty(a.Url))?.Url;

        if (previewUrl == null)
        {
            return null;
        }

        // only the start of the download is retried, the contents are read by the caller
        var response = await YandexDiskApiCalls.WrapAsync(_logger,
            () => _apiClient.OpenByUrlAsync(previewUrl, cancellationToken), cancellationToken);

        // disposing the contents ends the download and releases the connection, the response holds nothing else
        var contents = await response.Content.ReadAsStreamAsync(cancellationToken);

        return new SourcePreviewStream(contents, response.Content.Headers.ContentType?.MediaType ?? "image/jpeg");
    }

    private Task<Resource> GetResourceAsync(string reference, CancellationToken cancellationToken)
    {
        return YandexDiskApiCalls.WrapAsync(_logger,
            () => _apiClient.GetResourceAsync(reference, cancellationToken, limit: 1), cancellationToken);
    }

    private async Task<SourcePreview?> DownloadByUrlAsync(string? previewUrl, CancellationToken cancellationToken)
    {
        if (previewUrl == null)
        {
            return null;
        }

        var (contents, contentType) = await YandexDiskApiCalls.WrapAsync(_logger,
            () => _apiClient.DownloadByUrlAsync(previewUrl, cancellationToken), cancellationToken);

        return contents.Length == 0 ? null : new SourcePreview(contents, contentType ?? "image/jpeg");
    }

    public bool IsAuthError(Exception exception)
    {
        return exception is PhotoSourceException { IsAuthError: true };
    }

    public void Dispose()
    {
        // the HTTP client is not disposed here, its handler is pooled and reused by IHttpClientFactory
    }
}
