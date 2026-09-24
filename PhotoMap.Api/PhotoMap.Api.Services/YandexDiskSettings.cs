namespace PhotoMap.Api.Services;

public class YandexDiskSettings
{
    /// <summary>
    /// Download from the folder the Yandex.Disk apps upload camera photos to (Photostream), whatever it is called
    /// for the user. When false, <see cref="SourceFolder"/> is downloaded from.
    /// </summary>
    public bool UsePhotoStreamFolder { get; set; } = true;
    public string? SourceFolder { get; set; }
    /// <summary>
    /// How many files are listed at a time. The offset of a page is saved once the page has been processed, so
    /// this is how much of a stopped run is listed again when it resumes.
    /// </summary>
    public int DownloadLimit { get; set; } = 100;
    /// <summary>
    /// How many files of a page are downloaded at a time.
    /// </summary>
    public int MaxParallelDownloads { get; set; } = 4;
}
