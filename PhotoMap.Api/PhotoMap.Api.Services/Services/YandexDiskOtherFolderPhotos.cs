using Microsoft.Extensions.Logging;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The photos of a Yandex.Disk outside the folder it is processed from, wherever they are on it, and the preview
/// images Yandex.Disk makes of them.
/// </summary>
public class YandexDiskOtherFolderPhotos
{
    private const string ImageMediaType = "image";
    private const int PageSize = 500;

    private readonly ApiClient _apiClient;
    private readonly ILogger _logger;

    public YandexDiskOtherFolderPhotos(ApiClient apiClient, ILogger logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// Lists every photo of the disk of the formats processed, each once, but the ones right in the folder given: the
    /// photos of the folders within it are listed. The flat list of files cannot be sorted, so a run lists all of them
    /// and skips the ones saved already, rather than resume from an offset that new photos may have shifted. For the
    /// same reason a photo can come on two pages, the second time it is left out.
    /// </summary>
    /// <param name="excludedFolder">The folder the disk is processed from, such as <c>disk:/Camera Uploads/</c>.</param>
    public async Task<IReadOnlyList<Resource>> ListPhotosAsync(string excludedFolder, CancellationToken cancellationToken)
    {
        var folder = YandexDiskPaths.NormalizeFolder(excludedFolder);
        var photos = new List<Resource>();
        var resourceIds = new HashSet<string>();
        var offset = 0;

        while (true)
        {
            var page = await YandexDiskApiCalls.WrapAsync(_logger,
                () => _apiClient.GetFlatFilesListAsync(cancellationToken, ImageMediaType, PageSize, offset), cancellationToken);
            var items = page.Items ?? [];

            photos.AddRange(items.Where(a => a.Type == "file" &&
                                             SupportedImageFormats.IsSupported(a.Name) &&
                                             !string.Equals(YandexDiskPaths.GetFolder(a.Path), folder, StringComparison.Ordinal) &&
                                             resourceIds.Add(a.ResourceId)));
            offset += items.Length;

            if (items.Length == 0)
            {
                break;
            }
        }

        return photos;
    }

    /// <summary>
    /// The URL of the largest preview image of the photo, null when Yandex.Disk has made none (yet).
    /// </summary>
    public static string? GetPreviewUrl(Resource photo)
    {
        return YandexDiskVideoSource.GetPreviewUrl(photo) ?? photo.Preview;
    }

    /// <summary>
    /// Downloads the preview image as it is served, a JPEG whatever the format of the photo.
    /// </summary>
    public async Task<byte[]> DownloadPreviewAsync(string previewUrl, CancellationToken cancellationToken)
    {
        var (contents, _) = await YandexDiskApiCalls.WrapAsync(_logger,
            () => _apiClient.DownloadByUrlAsync(previewUrl, cancellationToken), cancellationToken);

        return contents;
    }
}
