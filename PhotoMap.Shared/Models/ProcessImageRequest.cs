namespace PhotoMap.Shared.Models;

public class ProcessImageRequest
{
    public DownloadedFileInfo DownloadedFileInfo { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public IEnumerable<int> Sizes { get; set; } = null!;
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    public string PhotoSourceName { get; set; } = null!;

    /// <summary>
    /// Goes with the result, <see cref="PhotoMap.Shared.Messages.PhotoProcessed.RequestId"/>, for the API to tell
    /// which request it answers. The request itself can then cross a process boundary, a callback could not.
    /// </summary>
    public Guid RequestId { get; set; }
}
