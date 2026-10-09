using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Shared.Yandex.Disk;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The files of the folder of a Yandex.Disk its photos are processed from, the Photostream folder unless the source
/// is set to another one.
/// </summary>
public sealed class YandexDiskSourceFiles : ISourceFiles
{
    private const int PageSize = 10000;
    private const string SortOrder = "created";

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

    public async Task<IReadOnlyList<SourceFile>> ListAsync(SourceDownloadContent content, string? folder,
        CancellationToken cancellationToken)
    {
        var sourceFolder = folder
            ?? await YandexDiskDownloadService.GetSourceFolderAsync(_apiClient, _settings, _logger, cancellationToken);

        var files = new List<SourceFile>();
        var offset = 0;

        while (true)
        {
            var resource = await YandexDiskApiCalls.WrapAsync(_logger,
                () => _apiClient.GetResourceAsync(sourceFolder, cancellationToken, offset, PageSize, SortOrder), cancellationToken);
            var page = resource.Embedded ?? throw new YandexDiskException($"{sourceFolder} is not a folder.");
            var items = page.Items ?? [];

            foreach (var item in items.Where(a => a.Type == "file"))
            {
                var fileContent = DownloadableFormats.GetContent(item.Name, item.MediaType);
                if (fileContent != null && content.HasFlag(fileContent.Value))
                {
                    files.Add(new SourceFile(item.Name, item.Path, item.Size, fileContent.Value));
                }
            }

            offset += items.Length;

            if (items.Length < PageSize || offset >= page.Total)
            {
                break;
            }
        }

        return files;
    }

    public async Task CopyToAsync(SourceFile file, Stream destination, CancellationToken cancellationToken)
    {
        using var response = await YandexDiskApiCalls.WrapAsync(_logger,
            () => _apiClient.OpenDownloadAsync(file.Path, null, cancellationToken), cancellationToken);

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
