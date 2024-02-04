using System.Threading.Channels;
using PhotoMap.Shared.Models;

namespace PhotoMap.Worker.Services;

public class RequestQueueService : IRequestQueueService
{
    private readonly Channel<ProcessImageRequest> _queue;

    public RequestQueueService()
    {
        BoundedChannelOptions options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<ProcessImageRequest>(options);
    }
    
    public async Task EnqueueAsync(ProcessImageRequest request)
    {
        await _queue.Writer.WriteAsync(request);
    }

    public async Task<ProcessImageRequest> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}