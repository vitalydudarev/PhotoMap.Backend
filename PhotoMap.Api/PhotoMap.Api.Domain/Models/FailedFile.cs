namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// A file of a photo source that could not be downloaded or processed, kept until it is saved by a later run.
/// </summary>
public class FailedFile
{
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    /// <summary>
    /// The ID the photo source assigned to the file (Dropbox file ID, Yandex.Disk resource_id).
    /// </summary>
    public required string ExternalId { get; set; }
    public string? Path { get; set; }
    public required string FileName { get; set; }
    public FileFailureStage Stage { get; set; }
    public required string Error { get; set; }
    /// <summary>
    /// How many times the file has failed, over all runs.
    /// </summary>
    public int Attempts { get; set; }
    public DateTimeOffset FailedAt { get; set; }
    /// <summary>
    /// The file is one of the other uploads of the source, outside the folder of its camera uploads, which a run of
    /// its own processes and retries.
    /// </summary>
    public bool FromOtherUploads { get; set; }
}
