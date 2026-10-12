using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Entities;

/// <summary>
/// The status of the processing of the photos of a photo source outside the folder it is processed from, kept apart
/// from the one of that folder: the two are processed by runs of their own.
/// </summary>
public class UserOtherFoldersStatusEntity
{
    public required long UserId { get; set; }
    public UserEntity? User { get; set; }
    public required long PhotoSourceId { get; set; }
    public PhotoSourceEntity? PhotoSource { get; set; }
    public PhotoSourceStatus Status { get; set; }
    public int TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public DateTimeOffset? LastUpdatedAt { get; set; }
}
