namespace PhotoMap.Api.Services;

/// <summary>
/// The settings of the Dropbox photo source. They are not read from the settings of the photo source in the
/// database, the defaults are used.
/// </summary>
public class DropboxSettings
{
    public string SourceFolder { get; set; } = "/Camera Uploads";

    /// <summary>
    /// How many files of a page are downloaded at a time. Dropbox rate limits per user and per app rather than
    /// per connection, but it does rate limit: too many at a time ends in waiting for 429s.
    /// </summary>
    public int MaxParallelDownloads { get; set; } = 4;
    /// <summary>
    /// How many files are downloaded at a time into the file storage, apart from the processing. Dropbox rate
    /// limits all the downloads of the user together, the processing's among them.
    /// </summary>
    public int MaxParallelFileDownloads { get; set; } = 8;
}