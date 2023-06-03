namespace PhotoMap.Api.Services.Services;

public interface IBackgroundTaskManager
{
    void Run(string taskName, Func<Task> taskFunction, CancellationTokenSource cancellationTokenSource);
    bool Cancel(string taskName);
}