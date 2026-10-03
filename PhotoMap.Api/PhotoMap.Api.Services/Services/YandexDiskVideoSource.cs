using Microsoft.Extensions.Logging;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The videos of a Yandex.Disk, wherever they are on it, and their preview images.
/// </summary>
public class YandexDiskVideoSource
{
    /// <summary>
    /// The sizes of the preview, in the order they are taken in: the largest Yandex.Disk makes first, then the
    /// smaller ones, for a video that has no preview of a larger size.
    /// </summary>
    public static readonly IReadOnlyList<string> PreviewSizeNames =
        ["XXXL", "XXL", "XL", "L", "M", "S", "XS", "XXS", "XXXS", "DEFAULT", "C"];

    private const string VideoMediaType = "video";
    private const int PageSize = 500;

    private readonly ApiClient _apiClient;
    private readonly ILogger _logger;

    public YandexDiskVideoSource(ApiClient apiClient, ILogger logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// Lists every video of the disk, each once. The flat list of files cannot be sorted, so a run lists all of them
    /// and skips the ones saved already, rather than resume from an offset that new videos may have shifted. For
    /// the same reason a video can come on two pages, the second time it is left out.
    /// </summary>
    public async Task<IReadOnlyList<Resource>> ListVideosAsync(CancellationToken cancellationToken)
    {
        var videos = new List<Resource>();
        var resourceIds = new HashSet<string>();
        var offset = 0;

        while (true)
        {
            var page = await YandexDiskApiCalls.WrapAsync(_logger,
                () => _apiClient.GetFlatFilesListAsync(cancellationToken, VideoMediaType, PageSize, offset), cancellationToken);
            var items = page.Items ?? [];

            videos.AddRange(items.Where(a => a.Type == "file" && resourceIds.Add(a.ResourceId)));
            offset += items.Length;

            if (items.Length == 0)
            {
                break;
            }
        }

        return videos;
    }

    /// <summary>
    /// The URL of the largest preview image of the video, of the sizes in <see cref="PreviewSizeNames"/>, null
    /// when Yandex.Disk has made none of them (yet).
    /// </summary>
    public static string? GetPreviewUrl(Resource video)
    {
        return PreviewSizeNames
            .Select(sizeName => video.Sizes.FirstOrDefault(a => a.Name == sizeName && !string.IsNullOrEmpty(a.Url))?.Url)
            .FirstOrDefault(url => url != null);
    }

    /// <summary>
    /// Downloads the preview image as it is served, in whatever format it is.
    /// </summary>
    public Task<(byte[] Contents, string? ContentType)> DownloadPreviewAsync(string previewUrl, CancellationToken cancellationToken)
    {
        return YandexDiskApiCalls.WrapAsync(_logger, () => _apiClient.DownloadByUrlAsync(previewUrl, cancellationToken),
            cancellationToken);
    }
}
