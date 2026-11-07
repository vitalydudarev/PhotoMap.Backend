using PhotoMap.Shared.Queues;

namespace PhotoMap.Shared.Messaging;

/// <summary>
/// Sends the messages through an in-process queue, when there is no broker: the sender and the receiver are one
/// object, registered as both.
/// </summary>
public class ChannelMessageTransport<T> : IMessageSender<T>, IMessageReceiver<T>
{
    private readonly ChannelMessageQueue<T> _queue;

    public ChannelMessageTransport(int capacity = 100)
    {
        _queue = new ChannelMessageQueue<T>(capacity);
    }

    public async Task SendAsync(T message, CancellationToken cancellationToken)
    {
        await _queue.EnqueueAsync(message, cancellationToken);
    }

    public async Task<IReadOnlyList<T>> ReceiveBatchAsync(int maxCount, TimeSpan maxWait, CancellationToken cancellationToken)
    {
        return await _queue.ReadBatchAsync(maxCount, maxWait, cancellationToken);
    }

    /// <summary>
    /// Nothing to do: a message read is gone from the queue.
    /// </summary>
    public void Acknowledge()
    {
    }
}
