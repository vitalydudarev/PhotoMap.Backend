using System.Runtime.CompilerServices;
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
    private const int PageSize = 10000;
    private const string SortOrder = "created";

    /// <summary>
    /// How long the download of a file waits to start, for its download URL and the headers of the response, before
    /// the attempt fails. Reading the contents takes as long as it does.
    /// </summary>
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How many times the download of a file is tried again when it fails.
    /// </summary>
    public const int DownloadMaxRetries = 2;

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
            await foreach (var item in ListFolderAsync(folderToList, cancellationToken))
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
        var files = new List<SourceFile>();

        if (content.HasFlag(SourceDownloadContent.Photos))
        {
            var photos = await otherUploads.ListPhotosAsync(sourceFolder, cancellationToken);
            files.AddRange(photos.Select(a => new SourceFile(a.Name, a.Path, a.Size, SourceDownloadContent.Photos)));
        }

        if (content.HasFlag(SourceDownloadContent.Videos))
        {
            var videos = await otherUploads.ListVideosAsync(sourceFolder, cancellationToken);
            files.AddRange(videos.Select(a => new SourceFile(a.Name, a.Path, a.Size, SourceDownloadContent.Videos)));
        }

        return files;
    }

    /// <summary>
    /// The files and folders directly within the folder, a page at a time.
    /// </summary>
    private async IAsyncEnumerable<Resource> ListFolderAsync(string folder,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var offset = 0;

        while (true)
        {
            var resource = await YandexDiskApiCalls.WrapAsync(_logger,
                () => _apiClient.GetResourceAsync(folder, cancellationToken, offset, PageSize, SortOrder), cancellationToken);
            var page = resource.Embedded ?? throw new YandexDiskException($"{folder} is not a folder.");
            var items = page.Items ?? [];

            foreach (var item in items)
            {
                yield return item;
            }

            offset += items.Length;

            if (items.Length < PageSize || offset >= page.Total)
            {
                yield break;
            }
        }
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
        var resource = await YandexDiskApiCalls.WrapAsync(_logger,
            () => _apiClient.GetResourceAsync(reference, cancellationToken, limit: 1), cancellationToken);

        var previewUrl = YandexDiskVideoSource.GetPreviewUrl(resource) ?? resource.Preview;
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
