namespace PhotoMap.Shared.Models;

public class DownloadedFileInfo
{
    public string ResourceName { get; set; }
    public string Path { get; set; }
    public DateTime? CreatedOn { get; set; }
    public string FileId { get; set; }

    /// <summary>
    /// The file is a photo outside the folder the source is processed from.
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