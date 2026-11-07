using PhotoMap.Api.Services.Exceptions;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Whether photos and videos can be processed. With Kafka enabled, the photos go through it, so they can be processed
/// only while it can be reached, which the API's monitor of the availability checks; the videos follow, so that
/// processing is available or not as a whole. With Kafka disabled, everything runs in the API and processing is
/// always available. The rest of the application works either way.
/// </summary>
public class ProcessingAvailability
{
    public const string KafkaUnreachableReason =
        "Kafka cannot be reached, so photos and videos cannot be processed for now. Everything else works. " +
        "Processing becomes available again as soon as Kafka is back.";

    public const string CheckingReason = "Checking the connection to Kafka.";

    private volatile State _state;

    /// <param name="checkedByMonitor">Kafka is enabled: unavailable until the monitor has found it reachable.</param>
    public ProcessingAvailability(bool checkedByMonitor)
    {
        _state = checkedByMonitor ? new State(false, CheckingReason) : new State(true, null);
    }

    public bool IsAvailable => _state.Available;

    /// <summary>
    /// Why processing is unavailable, null while it is available.
    /// </summary>
    public string? Reason => _state.Reason;

    /// <returns>Whether the availability, or the reason it is unavailable, has changed.</returns>
    public bool Set(bool available, string? reason)
    {
        var state = new State(available, available ? null : reason);
        var previous = _state;
        _state = state;

        return previous != state;
    }

    /// <exception cref="ProcessingUnavailableException">Processing is unavailable.</exception>
    public void EnsureAvailable()
    {
        var state = _state;
        if (!state.Available)
        {
            throw new ProcessingUnavailableException(state.Reason ?? "Processing is unavailable for now.");
        }
    }

    private sealed record State(bool Available, string? Reason);
}
