namespace PhotoMap.Api.Services;

public class YandexDiskDownloadState
{
    /// <summary>
    /// Offset in the folder, sorted by creation time, to continue listing from. 0 means listing from the start of
    /// the folder.
    /// </summary>
    public int Offset { get; set; }
}
