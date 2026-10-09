using System.Threading.Channels;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Tells the download of the previews of the failed files that files have failed. Signals given while it is busy come
/// down to one, which is enough: each round takes every file whose preview is missing.
/// </summary>
public class FailedPreviewsSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Notify()
    {
        _channel.Writer.TryWrite(true);
    }

    /// <summary>
    /// Waits for files to fail, returns right away if they have since the last wait.
    /// </summary>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _channel.Reader.ReadAsync(cancellationToken);
    }
}
