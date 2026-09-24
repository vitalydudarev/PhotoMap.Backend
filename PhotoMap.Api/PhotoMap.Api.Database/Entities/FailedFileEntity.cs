using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Entities;

public class FailedFileEntity
{
    public long Id { get; set; }
    public required long UserId { get; set; }
    public UserEntity? User { get; set; }
    public required long PhotoSourceId { get; set; }
    public PhotoSourceEntity? PhotoSource { get; set; }
    public required string ExternalId { get; set; }
    public string? Path { get; set; }
    public required string FileName { get; set; }
    public FileFailureStage Stage { get; set; }
    public required string Error { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset FailedAt { get; set; }
}
