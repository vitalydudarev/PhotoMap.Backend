using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Where the previews of the failed files are kept in the file storage.
/// </summary>
public static class FailedPreviewFiles
{
    /// <summary>
    /// Next to the video previews of the source: <c>Yandex.Disk/1/failed-previews/download-42.jpg</c>, a name of its
    /// own for each failed file.
    /// </summary>
    public static string GetPath(string photoSourceName, FailedPreview failedPreview, string? contentType)
    {
        var table = failedPreview.Table == FailedPreviewTable.FailedFiles ? "file" : "download";
        var extension = contentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };

        return Path.Combine(photoSourceName, failedPreview.UserId.ToString(), "failed-previews",
            $"{table}-{failedPreview.Id}{extension}");
    }

    /// <summary>
    /// Deletes the previews of files that no longer fail. A preview left behind is not worth failing over.
    /// </summary>
    public static void Delete(IFileStorage fileStorage, ILogger logger, IEnumerable<string?> previewFilePaths)
    {
        foreach (var previewFilePath in previewFilePaths.OfType<string>())
        {
            try
            {
                fileStorage.Delete(previewFilePath);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to delete the preview {PreviewFilePath} of a file that no longer fails",
                    previewFilePath);
            }
        }
    }
}
