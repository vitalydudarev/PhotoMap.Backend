namespace PhotoMap.Shared.Models;

public class DownloadedFileInfo
{
    public string ResourceName { get; set; }
    public string Path { get; set; }
    public DateTime? CreatedOn { get; set; }
    public string FileId { get; set; }

    public DownloadedFileInfo(string resourceName, string path, DateTime? createdOn, string fileId)
    {
        ResourceName = resourceName;
        Path = path;
        CreatedOn = createdOn;
        FileId = fileId;
    }
}