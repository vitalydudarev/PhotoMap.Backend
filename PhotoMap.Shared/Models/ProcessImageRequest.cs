namespace PhotoMap.Shared.Models;

public class ProcessImageRequest
{
    public DownloadedFileInfo DownloadedFileInfo { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public IEnumerable<int> Sizes { get; set; } = null!;
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    public string PhotoSourceName { get; set; } = null!;
}
