using System.Collections.Concurrent;
using PhotoMap.Shared.Models;

namespace PhotoMap.Worker.Services;

public class RequestQueueService : IRequestQueueService
{
    private readonly ConcurrentQueue<ProcessImageRequest> _queue = new ConcurrentQueue<ProcessImageRequest>();
    
    public void Enqueue(ProcessImageRequest request)
    {
        _queue.Enqueue(request);
    }

    public ProcessImageRequest? Dequeue()
    {
        return _queue.TryDequeue(out var request) ? request : null;
    }
}