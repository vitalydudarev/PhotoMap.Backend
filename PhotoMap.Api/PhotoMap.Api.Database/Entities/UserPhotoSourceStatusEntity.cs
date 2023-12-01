using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Entities;

public class UserPhotoSourceStatusEntity
{
    public required long UserId { get; set; }
    public UserEntity? User { get; set; }
    public required long PhotoSourceId { get; set; }
    public PhotoSourceEntity? PhotoSource { get; set; }
    public UserPhotoSourceStatus Status { get; set; }
    public int? TotalCount { get; set; }
    public int? ProcessedCount { get; set; }
    public int? FailedCount { get; set; }
    public int? LastProcessedFileIndex { get; set; }
    public DateTimeOffset? LastUpdatedAt { get; set; }
}
