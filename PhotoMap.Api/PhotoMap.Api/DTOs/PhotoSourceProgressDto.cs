namespace PhotoMap.Api.DTOs;

/// <summary>
/// The status of the processing of a photo source and its file counters, as last saved by a run.
/// </summary>
public class PhotoSourceProgressDto
{
    public UserPhotoSourceStatusDto Status { get; set; }
    public int TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public string? LastUpdatedAt { get; set; }
}
