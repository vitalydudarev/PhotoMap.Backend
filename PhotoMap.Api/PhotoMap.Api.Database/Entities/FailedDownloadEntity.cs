using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Entities;

public class FailedDownloadEntity
{
    public long Id { get; set; }
    public required long UserId { get; set; }
    public UserEntity? User { get; set; }
    public required long PhotoSourceId { get; set; }
    public PhotoSourceEntity? PhotoSource { get; set; }
    public SourceDownloadKind Kind { get; set; }
    public required string Folder { get; set; }
    public required string FileName { get; set; }
    public required string Path { get; set; }
    public long Size { get; set; }
    public SourceDownloadContent Content { get; set; }
    public required string Error { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset FailedAt { get; set; }
    public string? PreviewFilePath { get; set; }
    public string? PreviewContentType { get; set; }
    public string? PreviewError { get; set; }
    public int PreviewAttempts { get; set; }
    public DateTimeOffset? PreviewAttemptedAt { get; set; }
}
