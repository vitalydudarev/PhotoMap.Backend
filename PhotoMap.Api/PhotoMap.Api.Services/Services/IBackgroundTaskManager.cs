namespace PhotoMap.Api.Services.Services;

public interface IBackgroundTaskManager
{
    void AddTask(string taskName, Func<Task> taskFunction, CancellationTokenSource cancellationTokenSource);
    bool RemoveTask(string taskName);
    bool CancelTask(string taskName);
}