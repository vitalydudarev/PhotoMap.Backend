namespace PhotoMap.Api.Services;

/// <summary>
/// Formats the image processing pipeline can make thumbnails of: HEIC/HEIF are converted with Magick.NET, the rest
/// are decoded by SkiaSharp. Photo sources download only these files, other files (e.g. videos) are ignored.
/// </summary>
public static class SupportedImageFormats
{
    private static readonly Dictionary<string, string> ContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".webp"] = "image/webp",
        [".heic"] = "image/heic",
        [".heif"] = "image/heif"
    };

    public static bool IsSupported(string fileName)
    {
        return ContentTypesByExtension.ContainsKey(Path.GetExtension(fileName));
    }

    /// <summary>
    /// The media type a file of this name is served with, "application/octet-stream" for an unsupported format.
    /// </summary>
    public static string GetContentType(string fileName)
    {
        return ContentTypesByExtension.GetValueOrDefault(Path.GetExtension(fileName), "application/octet-stream");
    }
}
