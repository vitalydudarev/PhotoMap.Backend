namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// A video of a photo source. Only its preview image is stored by the application, the video itself stays in the
/// source.
/// </summary>
public class Video
{
    public long Id { get; set; }
    public required long UserId { get; set; }
    public required long PhotoSourceId { get; set; }

    /// <summary>
    /// The ID the photo source assigned to the file (Yandex.Disk resource_id).
    /// </summary>
    public required string ExternalId { get; set; }

    public required string FileName { get; set; }

    /// <summary>
    /// The folder of the video in the photo source, without its file name, such as <c>disk:/Camera Uploads</c>.
    /// </summary>
    public string? FolderPath { get; set; }

    public string? MimeType { get; set; }
    public long Size { get; set; }
    public DateTimeOffset DateTimeTaken { get; set; }

    /// <summary>
    /// The date the video was taken, as the photo source read it from the EXIF of the file, or else as the
    /// metadata of the file says; null when neither has it.
    /// </summary>
    public DateTimeOffset? ExifDateTime { get; set; }

    /// <summary>
    /// The GPS location, as the photo source read it, or else from the metadata of the file.
    /// </summary>
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>
    /// The names of the tags of the video, by name, which are those of the photos too; only loaded along with the
    /// videos of the Videos page.
    /// </summary>
    public IReadOnlyList<string> Tags { get; set; } = [];

    /// <summary>
    /// The metadata read from the file itself, as JSON, see <see cref="VideoExif.ExifString"/>; null until it has
    /// been read, which a run of the videos tries again.
    /// </summary>
    public string? ExifString { get; set; }

    /// <summary>
    /// The preview image as the photo source made it, stored without any processing.
    /// </summary>
    public string? PreviewFilePath { get; set; }

    public string? PreviewContentType { get; set; }
    public DateTimeOffset AddedOn { get; set; }

    /// <summary>
    /// The group of the videos of the user this one is a duplicate of, the ID of the first of them; null when it
    /// has no duplicate, or has not been looked at yet. See <c>VideoDuplicateFinder</c>.
    /// </summary>
    public long? DuplicateGroupId { get; set; }
}
