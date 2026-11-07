namespace PhotoMap.Shared.Messaging;

/// <summary>
/// Receives the messages of one kind, in batches, through whichever transport is registered: in memory, or Kafka.
/// One consumer uses it, one call at a time.
/// </summary>
/// <remarks>
/// Unlike <see cref="Queues.IMessageQueue{T}"/>, a message is not done with once it has been read: the consumer
/// tells when it has been handled, with <see cref="Acknowledge"/>. Kafka hands the messages not acknowledged again
/// after a restart (at-least-once); the in-memory transport loses them, with the rest of the queue.
/// </remarks>
public interface IMessageReceiver<T>
{
    /// <summary>
    /// Waits for a message, then takes the messages that arrive after it, until there are
    /// <paramref name="maxCount"/> of them or <paramref name="maxWait"/> has passed since the first one.
    /// </summary>
    /// <returns>The messages received, none when no more will come.</returns>
    Task<IReadOnlyList<T>> ReceiveBatchAsync(int maxCount, TimeSpan maxWait, CancellationToken cancellationToken);

    /// <summary>
    /// The messages received so far have been handled, they are not to be handed again.
    /// </summary>
    void Acknowledge();
}
