namespace PhotoMap.Shared.Models;

public class DownloadedFile
{
    public DownloadedFileInfo FileInfo { get; set; }
    public byte[] FileContents { get; set; }

    /// <summary>
    /// Completed when the file has gone through the processing pipeline: true if the photo was saved
    /// (or had already been saved), false if processing failed.
    /// </summary>
    public TaskCompletionSource<bool> Processed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    
    public DownloadedFile(DownloadedFileInfo fileInfo, byte[] fileContents)
    {
        FileInfo = fileInfo;
        FileContents = fileContents;
    }
}