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
    /// How many files are listed at a time.
    /// </summary>
    public int DownloadLimit { get; set; } = 10000;
    /// <summary>
    /// How many files of a page are processed before the offset reached is saved, which is how much of a stopped
    /// run is gone through again when it resumes. The files are processed that many at a time: the next ones start
    /// downloading once all of them have been saved.
    /// </summary>
    public int SaveStateEvery { get; set; } = 100;
    /// <summary>
    /// How many files of a page are downloaded at a time.
    /// </summary>
    public int MaxParallelDownloads { get; set; } = 4;
}
