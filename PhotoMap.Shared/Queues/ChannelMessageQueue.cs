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
}
