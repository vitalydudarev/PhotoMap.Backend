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
    /// The date the video was taken, as the photo source read it from the EXIF of the file, null when it has none.
    /// </summary>
    public DateTimeOffset? ExifDateTime { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>
    /// The preview image as the photo source made it, stored without any processing.
    /// </summary>
    public string? PreviewFilePath { get; set; }

    public string? PreviewContentType { get; set; }
    public DateTimeOffset AddedOn { get; set; }
}
