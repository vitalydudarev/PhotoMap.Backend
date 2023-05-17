namespace PhotoMap.Api.Services;

public class DropboxSettings
{
    public required string SourceFolder { get; set; }
    public int DownloadLimit { get; set; } = 2000;
}