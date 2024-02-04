namespace PhotoMap.Shared.Models;

public class DownloadedFile
{
    public DownloadedFileInfo FileInfo { get; set; }
    public byte[] FileContents { get; set; }
    
    public DownloadedFile(DownloadedFileInfo fileInfo, byte[] fileContents)
    {
        FileInfo = fileInfo;
        FileContents = fileContents;
    }
}