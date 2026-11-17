namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// The metadata read from the video file itself, such as the QuickTime metadata of an .mp4 or a .mov.
/// </summary>
/// <param name="ExifString">Every directory of the metadata, as JSON: an object of the directories, each an object
/// of its tags, as the EXIF of the photos is saved. An empty object when the file has no metadata that can be read.</param>
/// <param name="DateTimeTaken">When the video was taken, by the file, null when it does not say.</param>
/// <param name="Latitude">The latitude of the GPS location of the file, null when it has none.</param>
/// <param name="Longitude">The longitude of the GPS location of the file, null when it has none.</param>
public sealed record VideoExif(string ExifString, DateTimeOffset? DateTimeTaken, double? Latitude, double? Longitude)
{
    /// <summary>
    /// The metadata of a file of which none can be read, so that it is not read again.
    /// </summary>
    public static readonly VideoExif Empty = new("{}", null, null, null);

    /// <summary>
    /// Puts the metadata in the video. What the photo source has read of the file is kept, the file only fills in
    /// what it has not: the date it was taken and the GPS location.
    /// </summary>
    public void ApplyTo(Video video)
    {
        video.ExifString = ExifString;

        if (video.ExifDateTime == null && DateTimeTaken is { } dateTimeTaken)
        {
            video.ExifDateTime = dateTimeTaken;
            video.DateTimeTaken = dateTimeTaken;
        }

        if (video.Latitude == null && video.Longitude == null && Latitude != null && Longitude != null)
        {
            video.Latitude = Latitude;
            video.Longitude = Longitude;
        }
    }
}
