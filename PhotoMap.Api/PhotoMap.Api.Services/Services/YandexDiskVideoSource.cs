using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The videos of a Yandex.Disk, wherever they are on it, their preview images and their contents.
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
    /// <param name="listed">Told how many videos have been found so far, after each page.</param>
    public async Task<IReadOnlyList<Resource>> ListVideosAsync(CancellationToken cancellationToken, Action<int>? listed = null)
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
            listed?.Invoke(videos.Count);

            if (items.Length == 0)
            {
                break;
            }
        }

        return videos;
    }

    /// <summary>
    /// Looks a video up by its path again, to retry one that failed with the details the listing gave, its previews
    /// among them.
    /// </summary>
    public Task<Resource> GetVideoAsync(string path, CancellationToken cancellationToken)
    {
        return YandexDiskApiCalls.WrapAsync(_logger, () => _apiClient.GetResourceAsync(path, cancellationToken, limit: 1),
            cancellationToken);
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

    /// <summary>
    /// Starts downloading the video, or the range of it asked for, by its path, as photos are downloaded. The
    /// caller reads the contents as they come, and disposes the response.
    /// </summary>
    /// <param name="range">The bytes to download, the whole video when null.</param>
    public Task<HttpResponseMessage> OpenVideoAsync(string path, RangeHeaderValue? range, CancellationToken cancellationToken)
    {
        return YandexDiskApiCalls.WrapAsync(_logger, () => _apiClient.OpenDownloadAsync(path, range, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc cref="ReadExifAsync(string, long, CancellationToken)"/>
    public Task<VideoExif> ReadExifAsync(Resource video, CancellationToken cancellationToken)
    {
        return ReadExifAsync(video.Path, video.Size, cancellationToken);
    }

    /// <summary>
    /// Reads the metadata of the video file, downloading only the parts of it that hold the metadata, see
    /// <see cref="RangeReadStream"/>. When the server does not send parts of the file, all of it is downloaded.
    /// </summary>
    /// <param name="path">The path of the video on the disk.</param>
    /// <param name="size">The size of the file.</param>
    /// <exception cref="PhotoMap.Api.Services.Exceptions.YandexDiskException">The file cannot be downloaded.</exception>
    public async Task<VideoExif> ReadExifAsync(string path, long size, CancellationToken cancellationToken)
    {
        // the download URL serves any range of the file, so it is asked for once for all of them
        var downloadUrl = await YandexDiskApiCalls.WrapAsync(_logger,
            () => _apiClient.GetDownloadUrlAsync(path, cancellationToken), cancellationToken);

        await using var stream = new RangeReadStream(
            (offset, count, token) => YandexDiskApiCalls.WrapAsync(_logger,
                () => _apiClient.DownloadRangeAsync(downloadUrl.Href, offset, count, token), token),
            size, cancellationToken);

        try
        {
            // the reading waits for the downloads, it is not left to block the thread of the caller
            var exif = await Task.Run(() => ReadExif(stream), cancellationToken);

            _logger.LogDebug("The metadata of {Path} has been read in {DownloadCount} downloads", path, stream.DownloadCount);

            return exif;
        }
        catch (RangeNotSupportedException e)
        {
            _logger.LogInformation("The parts of {Path} cannot be downloaded ({ErrorMessage}), downloading all of it",
                path, e.Message);

            return await ReadExifOfWholeFileAsync(path, cancellationToken);
        }
    }

    /// <summary>
    /// Downloads the whole video to a temporary file, deleted once the metadata has been read: the reader seeks in
    /// the file, which the download cannot do, and a video may be too large to keep in memory.
    /// </summary>
    private async Task<VideoExif> ReadExifOfWholeFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(System.IO.Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite,
            FileShare.None, bufferSize: 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        // the download is made again from the start when it fails partway
        await YandexDiskApiCalls.WrapAsync(_logger, async () =>
        {
            file.SetLength(0);

            using var response = await _apiClient.OpenDownloadAsync(path, null, cancellationToken);
            await using var contents = await response.Content.ReadAsStreamAsync(cancellationToken);
            await contents.CopyToAsync(file, cancellationToken);

            return true;
        }, cancellationToken);

        _logger.LogDebug("{Path} has been downloaded whole, {Size} bytes, to read its metadata", path, file.Length);

        file.Position = 0;

        return await Task.Run(() => VideoExifReader.Read(file), cancellationToken);
    }

    private static VideoExif ReadExif(RangeReadStream stream)
    {
        VideoExif exif;
        try
        {
            exif = VideoExifReader.Read(stream);
        }
        catch when (stream.DownloadError != null)
        {
            ExceptionDispatchInfo.Throw(stream.DownloadError);
            throw;
        }

        // the reader may have taken a download that failed as the end of the file, and read only part of the metadata
        if (stream.DownloadError != null)
        {
            ExceptionDispatchInfo.Throw(stream.DownloadError);
        }

        return exif;
    }

    /// <summary>
    /// The path of the video on the disk, from its folder and file name.
    /// </summary>
    public static string GetPath(string folderPath, string fileName)
    {
        return folderPath.EndsWith('/') ? folderPath + fileName : $"{folderPath}/{fileName}";
    }
}
