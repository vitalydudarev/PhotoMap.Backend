namespace PhotoMap.Api.Services;

/// <summary>
/// Formats the image processing pipeline can make thumbnails of: HEIC/HEIF are converted with Magick.NET, the rest
/// are decoded by SkiaSharp. Photo sources download only these files, other files (e.g. videos) are ignored.
/// </summary>
public static class SupportedImageFormats
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic", ".heif"
    };

    public static bool IsSupported(string fileName)
    {
        return Extensions.Contains(Path.GetExtension(fileName));
    }
}
