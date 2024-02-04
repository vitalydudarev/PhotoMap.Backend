namespace PhotoMap.Shared.Models;

public class ProcessImageRequest
{
    public DownloadedFileInfo DownloadedFileInfo { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public IEnumerable<int> Sizes { get; set; } = null!;
}