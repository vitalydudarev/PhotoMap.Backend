namespace PhotoMap.Api.Services.Exceptions;

/// <summary>
/// Photos and videos cannot be processed for now, e.g. Kafka cannot be reached; the rest of the application works.
/// Answered with 503 Service Unavailable.
/// </summary>
public class ProcessingUnavailableException : Exception
{
    public ProcessingUnavailableException(string message) : base(message)
    {
    }
}
