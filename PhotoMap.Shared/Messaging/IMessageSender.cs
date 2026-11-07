namespace PhotoMap.Shared.Messaging;

/// <summary>
/// Sends messages of one kind to their consumer, through whichever transport is registered: in memory, or Kafka.
/// </summary>
public interface IMessageSender<in T>
{
    /// <summary>
    /// Completes once the transport has the message: the broker has acknowledged it, or it is in the queue.
    /// </summary>
    /// <exception cref="MessagePublishException">The message has not been delivered.</exception>
    Task SendAsync(T message, CancellationToken cancellationToken);
}
