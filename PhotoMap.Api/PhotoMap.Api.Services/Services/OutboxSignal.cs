using System.Threading.Channels;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Tells the outbox relay that messages have been added, so that they are published right away rather than at the
/// next poll. Signals given while it is busy come down to one, which is enough: each round takes every message
/// waiting.
/// </summary>
public class OutboxSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Notify()
    {
        _channel.Writer.TryWrite(true);
    }

    /// <summary>
    /// Waits for messages to be added, returns right away if they have been since the last wait.
    /// </summary>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _channel.Reader.ReadAsync(cancellationToken);
    }
}
