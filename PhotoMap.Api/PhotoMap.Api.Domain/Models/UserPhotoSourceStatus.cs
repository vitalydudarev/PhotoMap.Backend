namespace PhotoMap.Api.Domain.Models;

public class UserPhotoSourceStatus
{
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    public PhotoSourceStatus Status { get; set; }
    public int TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public DateTimeOffset? LastUpdatedAt { get; set; }
    /// <summary>
    /// When the last run listing the source that has finished started: the files uploaded before it are the ones
    /// the runs have been through, which a sync checks. Null when no such run has finished, and on the reports of
    /// any other run, which leave the one saved as it is.
    /// </summary>
    public DateTimeOffset? LastListedAt { get; set; }

    /// <summary>
    /// The files a run loading the list of the files it takes has found so far, null when it is not loading it. Sent
    /// to the frontend only, it is not saved.
    /// </summary>
    public int? ListedCount { get; set; }
}