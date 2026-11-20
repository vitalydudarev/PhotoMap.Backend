using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.DTOs;

public class FailedFileDto
{
    public required string ExternalId { get; set; }
    public string? Path { get; set; }
    public required string FileName { get; set; }
    /// <summary>
    /// Download or Processing.
    /// </summary>
    public required string Stage { get; set; }
    public required string Error { get; set; }
    public int Attempts { get; set; }
    public required string FailedAt { get; set; }

    public static FailedFileDto From(FailedFile failedFile)
    {
        return new FailedFileDto
        {
            ExternalId = failedFile.ExternalId,
            Path = failedFile.Path,
            FileName = failedFile.FileName,
            Stage = failedFile.Stage.ToString(),
            Error = failedFile.Error,
            Attempts = failedFile.Attempts,
            FailedAt = failedFile.FailedAt.UtcDateTime.ToString("o")
        };
    }
}
