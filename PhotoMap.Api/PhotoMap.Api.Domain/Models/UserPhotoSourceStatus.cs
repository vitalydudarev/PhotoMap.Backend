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
}