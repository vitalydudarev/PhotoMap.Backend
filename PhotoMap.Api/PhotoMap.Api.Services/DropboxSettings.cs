namespace PhotoMap.Api.Services;

public class DropboxSettings
{
    public required string SourceFolder { get; set; }
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