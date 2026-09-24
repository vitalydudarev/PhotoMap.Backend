using System.Threading.Channels;

namespace PhotoMap.Shared.Queues;

public class ChannelMessageQueue<T> : IMessageQueue<T>
{
    private readonly Channel<T> _channel;

    public ChannelMessageQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };

        _channel = Channel.CreateBounded<T>(options);
    }

    public ValueTask EnqueueAsync(T message, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(message, cancellationToken);
    }

    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<T>> ReadBatchAsync(int maxCount, TimeSpan maxWait,
        CancellationToken cancellationToken = default)
    {
        var reader = _channel.Reader;
        var messages = new List<T>();

        if (!await reader.WaitToReadAsync(cancellationToken))
        {
            return messages;
        }

        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        waitCancellation.CancelAfter(maxWait);

        while (messages.Count < maxCount)
        {
            if (reader.TryRead(out var message))
            {
                messages.Add(message);
                continue;
            }

            try
            {
                if (!await reader.WaitToReadAsync(waitCancellation.Token))
                {
                    // completed, the messages read so far are the last ones
                    break;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // the time to wait for more is up
                break;
            }
        }

        return messages;
    }
}
