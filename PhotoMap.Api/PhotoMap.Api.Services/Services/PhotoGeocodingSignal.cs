using System.Threading.Channels;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Tells the geocoding that photos were saved. Signals given while it is busy come down to one, which is enough: each
/// round geocodes every photo that is not geocoded yet.
/// </summary>
public class PhotoGeocodingSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Notify()
    {
        _channel.Writer.TryWrite(true);
    }

    /// <summary>
    /// Waits for photos to be saved, returns right away if they were since the last wait.
    /// </summary>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _channel.Reader.ReadAsync(cancellationToken);
    }
}
