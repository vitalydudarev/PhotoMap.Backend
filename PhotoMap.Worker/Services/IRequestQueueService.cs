using PhotoMap.Shared.Models;

namespace PhotoMap.Worker.Services;

public interface IRequestQueueService
{
    Task EnqueueAsync(ProcessImageRequest request);
    Task<ProcessImageRequest> DequeueAsync(CancellationToken cancellationToken = default);
}