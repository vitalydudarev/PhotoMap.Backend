using PhotoMap.Shared.Models;

namespace PhotoMap.Worker.Services;

public interface IRequestQueueService
{
    void Enqueue(ProcessImageRequest request);
    ProcessImageRequest? Dequeue();
}