namespace PhotoMap.Shared.Queues;

/// <summary>
/// In-process queue used instead of an external broker: producers enqueue messages,
/// a background service on the consuming side reads them.
/// </summary>
public interface IMessageQueue<T>
{
    ValueTask EnqueueAsync(T message, CancellationToken cancellationToken = default);

    IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default);
}
