using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services;

/// <summary>
/// The files a download of a photo source takes, by their extension: the photos are the formats the photos are
/// processed in and a few more a camera saves, the videos the usual container formats. A source that tells what a
/// file is, as Yandex.Disk does, is taken at its word for a file of any other extension.
/// </summary>
public static class DownloadableFormats
{
    private static readonly HashSet<string> OtherPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tif", ".tiff", ".dng"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".m4v", ".avi", ".mkv", ".webm", ".3gp", ".mts", ".m2ts", ".wmv", ".mpg", ".mpeg"
    };

    /// <param name="fileName">The name of the file, with its extension.</param>
    /// <param name="mediaType">What the source says the file is, "image" or "video", when it says.</param>
    /// <returns>Whether the file is a photo or a video, null when it is neither.</returns>
    public static SourceDownloadContent? GetContent(string fileName, string? mediaType = null)
    {
        var extension = Path.GetExtension(fileName);

        if (SupportedImageFormats.IsSupported(fileName) || OtherPhotoExtensions.Contains(extension) || mediaType == "image")
        {
            return SourceDownloadContent.Photos;
        }

        if (VideoExtensions.Contains(extension) || mediaType == "video")
        {
            return SourceDownloadContent.Videos;
        }

        return null;
    }
}
