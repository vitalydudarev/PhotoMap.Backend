using PhotoMap.Worker.Models.Image;

namespace PhotoMap.Worker.Helpers;

public static class ExifHelper
{
    /// <summary>
    /// The GPS time some apps, Instagram among them, write when they have none: it tells nothing about the photo.
    /// </summary>
    public static readonly DateTime NoGpsTime = DateTime.UnixEpoch;

    public static DateTime? GetDate(ExifData exif)
    {
        var gpsTime = exif.Gps?.DateTimeStamp?.ToUniversalTime();
        if (gpsTime == NoGpsTime)
        {
            gpsTime = null;
        }

        return gpsTime ?? exif.ExifSubIfd?.DateTimeOriginal?.ToUniversalTime();
    }

    public static double? GetLatitude(ExifData exif)
    {
        var gps = exif.Gps;
        if (gps != null && gps.Latitude != null && gps.LatitudeRef != null)
        {
            int multiplier = gps.LatitudeRef == "S" ? -1 : 1;
            return multiplier * (gps.Latitude.Degrees + gps.Latitude.Minutes / 60 + gps.Latitude.Seconds / 3600);
        }

        return null;
    }

    public static double? GetLongitude(ExifData exif)
    {
        var gps = exif.Gps;
        if (gps != null && gps.Longitude != null && gps.LongitudeRef != null)
        {
            int multiplier = gps.LongitudeRef == "W" ? -1 : 1;
            return multiplier * (gps.Longitude.Degrees + gps.Longitude.Minutes / 60 + gps.Longitude.Seconds / 3600);
        }

        return null;
    }
}