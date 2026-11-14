using System.Text.Json;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Tells when a photo was really taken, when it was saved as taken at the GPS time of its EXIF, and that time is the
/// one some apps, Instagram among them, write when they have none: 1970-01-01 00:00:00 UTC. The time it was taken is
/// then the one of the camera, DateTimeOriginal.
/// </summary>
public static class PhotoDateTakenFixer
{
    /// <summary>
    /// The GPS time written by the apps that have none.
    /// </summary>
    public static readonly DateTimeOffset NoGpsTime = DateTimeOffset.UnixEpoch;

    /// <param name="exifString">The EXIF of the photo, as the worker serialized it to JSON.</param>
    /// <returns>When the photo was taken, in UTC, or null if its GPS time is not <see cref="NoGpsTime"/>, or its EXIF
    /// does not tell when it was taken otherwise.</returns>
    public static DateTimeOffset? GetDateTaken(string? exifString)
    {
        if (string.IsNullOrEmpty(exifString))
        {
            return null;
        }

        try
        {
            using var exif = JsonDocument.Parse(exifString);

            if (GetDateTime(exif.RootElement, "Gps", "DateTimeStamp") != NoGpsTime)
            {
                return null;
            }

            // the worker saved it with the offset of its time zone, which it read the time in
            var taken = GetDateTime(exif.RootElement, "ExifSubIfd", "DateTimeOriginal");

            return taken == null || taken == NoGpsTime ? null : taken.Value.ToUniversalTime();
        }
        catch (JsonException)
        {
            // EXIF that cannot be read tells nothing about the photo
            return null;
        }
    }

    private static DateTimeOffset? GetDateTime(JsonElement root, string directory, string tag)
    {
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty(directory, out var directoryElement) &&
               directoryElement.ValueKind == JsonValueKind.Object &&
               directoryElement.TryGetProperty(tag, out var value) &&
               value.ValueKind == JsonValueKind.String &&
               value.TryGetDateTimeOffset(out var dateTime)
            ? dateTime
            : null;
    }
}
