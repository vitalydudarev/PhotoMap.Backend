namespace PhotoMap.Shared.Events;

/// <summary>
/// A photo has been processed and saved to the database. Published to <see cref="Topic"/>, keyed by
/// <see cref="GetKey"/>.
/// </summary>
/// <remarks>
/// The contract between the publisher and every consumer, whatever its language: new fields can be added, the
/// consumers ignore the ones they do not know, but renaming or removing one breaks them.
/// </remarks>
public sealed record PhotoSavedEvent(
    long UserId,
    long PhotoSourceId,
    string PhotoSourceName,
    string FileName,
    string? Path,
    string? ExternalId,
    string? ContentHash,
    DateTimeOffset DateTimeTaken,
    double? Latitude,
    double? Longitude,
    bool FromOtherUploads,
    DateTimeOffset SavedOn)
{
    public const string Topic = "photo-saved";

    /// <summary>
    /// The photos of one source of a user go to one partition, so they are consumed in the order they were saved.
    /// The photo source alone would put the photos of every user of e.g. Dropbox on one partition.
    /// </summary>
    public string GetKey() => $"{UserId}:{PhotoSourceId}";
}
