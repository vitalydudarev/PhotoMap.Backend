namespace PhotoMap.Api.DTOs;

/// <summary>
/// Whether photos and videos can be processed; the rest of the application works either way.
/// </summary>
public class ProcessingAvailabilityDto
{
    public bool Available { get; set; }

    /// <summary>
    /// Why processing is unavailable, e.g. Kafka cannot be reached; null while it is available.
    /// </summary>
    public string? Reason { get; set; }
}
