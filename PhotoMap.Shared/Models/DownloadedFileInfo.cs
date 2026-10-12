namespace PhotoMap.Shared.Models;

public class DownloadedFileInfo
{
    public string ResourceName { get; set; }
    public string Path { get; set; }
    public DateTime? CreatedOn { get; set; }
    public string FileId { get; set; }

    /// <summary>
    /// The GPS location the photo source has read from the photo, taken when the downloaded file has none of its
    /// own, as a preview the source has made of the photo may not.
    /// </summary>
    public double? Latitude { get; init; }

    /// <inheritdoc cref="Latitude"/>
    public double? Longitude { get; init; }

    /// <summary>
    /// The SHA-256 of the contents of the original file, as the photo source tells it, for a file downloaded that is
    /// not the original: the photo then has the same hash as if the original had been downloaded.
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>
    /// The file is a preview of a photo outside the folder the source is processed from.
    /// </summary>
    public bool FromOtherFolder { get; init; }

    public DownloadedFileInfo(string resourceName, string path, DateTime? createdOn, string fileId)
    {
        ResourceName = resourceName;
        Path = path;
        CreatedOn = createdOn;
        FileId = fileId;
    }
}