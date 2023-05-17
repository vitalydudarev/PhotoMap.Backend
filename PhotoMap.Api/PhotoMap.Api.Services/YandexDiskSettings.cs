namespace PhotoMap.Api.Services;

public class YandexDiskSettings
{
    public bool UsePhotoStreamFolder { get; set; } = true;
    public string? SourceFolder { get; set; }
    public int DownloadLimit { get; set; } = 100;
}