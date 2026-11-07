using System.Collections.Concurrent;
using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The images handed over to be processed whose runs wait for them, by the ids of their requests: a request, and its
/// result, carry only the id, from which the result is matched back to what waits for it (request/reply over
/// messages, the id being the correlation id).
/// </summary>
/// <remarks>
/// Kept in memory: after a restart, the results of the requests made before it find nothing waiting for them. The
/// photos are still saved, the runs of then having been paused and counting them as they resume.
/// </remarks>
public class ProcessingRequests
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ProcessingResult>> _pending = new();

    public int PendingCount => _pending.Count;

    /// <param name="processed">Completed with the result of the request.</param>
    /// <returns>The id the request is to carry.</returns>
    public Guid Register(TaskCompletionSource<ProcessingResult> processed)
    {
        var requestId = Guid.NewGuid();
        _pending[requestId] = processed;

        return requestId;
    }

    /// <summary>
    /// The request has not been sent, no result will come: what waits for it is left waiting, as when the run is
    /// stopped.
    /// </summary>
    public void Forget(Guid requestId)
    {
        _pending.TryRemove(requestId, out _);
    }

    /// <returns>Whether anything waited for the result.</returns>
    public bool Complete(Guid requestId, ProcessingResult result)
    {
        if (!_pending.TryRemove(requestId, out var processed))
        {
            return false;
        }

        processed.TrySetResult(result);
        return true;
    }
}
