namespace PhotoMap.Shared.Models;

/// <summary>
/// An image downloaded by the API, for the worker to process. Sent through Kafka's <see cref="Topic"/> when Kafka
/// is enabled, an in-process queue otherwise.
/// </summary>
public class ProcessImageRequest
{
    public const string Topic = "image-processing-requests";

    public DownloadedFileInfo DownloadedFileInfo { get; set; } = null!;

    /// <summary>
    /// The path of the downloaded file on the disk: the worker reads the image from there rather than from the
    /// message (the claim-check pattern), so the API and the worker must see the same disk.
    /// </summary>
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

    /// <summary>
    /// The request id, random: the requests are spread over all the partitions, so that as many workers as there are
    /// partitions share them. The order they are processed in does not matter, unlike that of the events of
    /// <see cref="Events.PhotoSavedEvent"/>, keyed so that those of one photo source keep it.
    /// </summary>
    public string GetKey() => RequestId.ToString("N");
}
