using Microsoft.Extensions.Logging;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The photos of a Yandex.Disk outside the folder it is processed from, wherever they are on it.
/// </summary>
public class YandexDiskOtherUploads
{
    private const string DiskRoot = "disk:/";

    /// <summary>
    /// The photos of this folder, and of the folders within it, are not other uploads, they are left out.
    /// </summary>
    private const string ExcludedFolder = "disk:/Фотокамера/";

    private readonly ApiClient _apiClient;
    private readonly ILogger _logger;

    public YandexDiskOtherUploads(ApiClient apiClient, ILogger logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// Lists every photo of the disk of the formats processed, each once, but the ones right in the folder given: the
    /// photos of the folders within it are listed. The photos of <see cref="ExcludedFolder"/>, and of the folders
    /// within it, are left out too. See <see cref="ListFilesAsync"/> for how the disk is listed.
    /// </summary>
    /// <param name="excludedFolder">The folder the disk is processed from, such as <c>disk:/Camera Uploads/</c>.</param>
    public async Task<IReadOnlyList<Resource>> ListPhotosAsync(string excludedFolder, CancellationToken cancellationToken)
    {
        return (await ListFilesAsync(excludedFolder, cancellationToken)).Where(IsPhoto).ToList();
    }

    /// <summary>
    /// Lists every video of the disk outside the folder given, as <see cref="ListPhotosAsync"/> does the photos. The
    /// videos are not imported as other uploads, only downloaded along with them.
    /// </summary>
    /// <param name="excludedFolder">The folder the disk is processed from, such as <c>disk:/Camera Uploads/</c>.</param>
    public async Task<IReadOnlyList<Resource>> ListVideosAsync(string excludedFolder, CancellationToken cancellationToken)
    {
        return (await ListFilesAsync(excludedFolder, cancellationToken)).Where(IsVideo).ToList();
    }

    /// <summary>
    /// Lists every file of the disk outside the folder given and <see cref="ExcludedFolder"/>, each once, by walking
    /// its folders from the root, a folder before the ones within it. The flat list of the files of the disk is not
    /// used: it leaves out photos of some folders, such as those of the Загрузки folder, which only the listing of
    /// their folders gives. The folders within <see cref="ExcludedFolder"/> are not listed at all. A run lists all the
    /// files and skips the ones saved already, rather than resume from where it stopped, which new files may have
    /// shifted.
    /// </summary>
    /// <param name="excludedFolder">The folder the disk is processed from: its files are left out, the files of the
    /// folders within it are not.</param>
    public async Task<IReadOnlyList<Resource>> ListFilesAsync(string excludedFolder, CancellationToken cancellationToken)
    {
        var folder = YandexDiskPaths.NormalizeFolder(excludedFolder);
        var files = new List<Resource>();
        var resourceIds = new HashSet<string>();
        var folders = new Queue<string>([DiskRoot]);
        var listedFolders = 0;

        while (folders.TryDequeue(out var folderToList))
        {
            listedFolders++;

            await foreach (var item in YandexDiskFolders.ListFolderAsync(_apiClient, _logger, folderToList, cancellationToken))
            {
                if (item.Type == "dir")
                {
                    if (!IsInExcludedFolder(item.Path))
                    {
                        folders.Enqueue(item.Path);
                    }
                }
                else if (item.Type == "file" &&
                         !string.Equals(YandexDiskPaths.GetFolder(item.Path), folder, StringComparison.Ordinal) &&
                         !IsInExcludedFolder(item.Path) &&
                         resourceIds.Add(item.ResourceId))
                {
                    files.Add(item);
                }
            }
        }

        _logger.LogInformation("{FileCount} files outside {SourceFolder} in {FolderCount} folders of the disk",
            files.Count, excludedFolder, listedFolders);

        return files;
    }

    /// <summary>
    /// Whether the file is a photo of a format processed.
    /// </summary>
    public static bool IsPhoto(Resource file)
    {
        return SupportedImageFormats.IsSupported(file.Name);
    }

    /// <summary>
    /// Whether the file is a video that is downloaded.
    /// </summary>
    public static bool IsVideo(Resource file)
    {
        return DownloadableFormats.GetContent(file.Name, file.MediaType) == SourceDownloadContent.Videos;
    }

    /// <summary>
    /// Whether the file or folder is <see cref="ExcludedFolder"/>, or within it.
    /// </summary>
    private static bool IsInExcludedFolder(string path)
    {
        return (path.TrimEnd('/') + "/").StartsWith(ExcludedFolder, StringComparison.Ordinal);
    }

    /// <summary>
    /// Looks a photo up by its path again, to retry one that failed with the details the listing gave.
    /// </summary>
    public Task<Resource> GetPhotoAsync(string path, CancellationToken cancellationToken)
    {
        return YandexDiskApiCalls.WrapAsync(_logger, () => _apiClient.GetResourceAsync(path, cancellationToken, limit: 1),
            cancellationToken);
    }

    /// <summary>
    /// Downloads the photo itself, with its EXIF, as the photos of the folder the disk is processed from are.
    /// </summary>
    public Task<byte[]> DownloadPhotoAsync(Resource photo, CancellationToken cancellationToken)
    {
        return YandexDiskApiCalls.WrapAsync(_logger, () => _apiClient.DownloadFileAsync(photo.Path, cancellationToken),
            cancellationToken);
    }
}
