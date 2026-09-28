using System.Text.Json;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The rules that put a photo in its categories, from its file name and the EXIF the worker read from it.
/// </summary>
public static class PhotoCategorizer
{
    /// <summary>
    /// The version of the rules, saved with the categories of each photo. Changing the rules takes a new version, so
    /// that the photos put in their categories by the old rules are put in them again.
    /// </summary>
    public const int Version = 1;

    private const string DroneMake = "DJI";
    private const int DroneWidth = 4000;
    private const int DroneHeight = 3000;

    /// <param name="fileName">The name of the photo file, with its extension.</param>
    /// <param name="exifString">The EXIF of the photo, as the worker serialized it to JSON.</param>
    public static IReadOnlyCollection<PhotoCategory> Categorize(string fileName, string? exifString)
    {
        var categories = new List<PhotoCategory>();

        if (Path.GetExtension(fileName).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(PhotoCategory.Screenshot);
        }

        if (IsDroneFootage(exifString))
        {
            categories.Add(PhotoCategory.DroneFootage);
        }

        return categories;
    }

    private static bool IsDroneFootage(string? exifString)
    {
        if (string.IsNullOrEmpty(exifString))
        {
            return false;
        }

        try
        {
            using var exif = JsonDocument.Parse(exifString);

            var make = GetProperty(exif.RootElement, "ExifIfd0", "Make");
            if (make?.ValueKind == JsonValueKind.String &&
                make.Value.GetString()!.Trim().Equals(DroneMake, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return GetInt(exif.RootElement, "ExifSubIfd", "Width") == DroneWidth &&
                   GetInt(exif.RootElement, "ExifSubIfd", "Height") == DroneHeight;
        }
        catch (JsonException)
        {
            // EXIF that cannot be read tells nothing about the photo
            return false;
        }
    }

    private static int? GetInt(JsonElement root, string directory, string tag)
    {
        var value = GetProperty(root, directory, tag);

        return value?.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var number) ? number : null;
    }

    private static JsonElement? GetProperty(JsonElement root, string directory, string tag)
    {
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty(directory, out var directoryElement) &&
               directoryElement.ValueKind == JsonValueKind.Object &&
               directoryElement.TryGetProperty(tag, out var value)
            ? value
            : null;
    }
}
