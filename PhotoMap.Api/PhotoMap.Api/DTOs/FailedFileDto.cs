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
}
