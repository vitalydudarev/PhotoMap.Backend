namespace PhotoMap.Shared.Queues;

/// <summary>
/// In-process queue used instead of an external broker: producers enqueue messages,
/// a background service on the consuming side reads them.
/// </summary>
public interface IMessageQueue<T>
{
    ValueTask EnqueueAsync(T message, CancellationToken cancellationToken = default);

    IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for a message, then takes the messages that arrive after it, until there are
    /// <paramref name="maxCount"/> of them or <paramref name="maxWait"/> has passed since the first one.
    /// </summary>
    /// <returns>The messages read, none when the queue has been completed.</returns>
    ValueTask<IReadOnlyList<T>> ReadBatchAsync(int maxCount, TimeSpan maxWait, CancellationToken cancellationToken = default);
}
