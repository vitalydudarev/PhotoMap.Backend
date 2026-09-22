namespace PhotoMap.Api.Services.Services;

public interface IBackgroundTaskManager
{
    /// <summary>
    /// Starts the task unless a task with the same name is running (or still stopping).
    /// </summary>
    /// <returns>false if a task with the same name is running.</returns>
    bool TryStartTask(string taskName, Func<CancellationToken, Task> taskFunction);

    bool IsRunning(string taskName);

    /// <summary>
    /// Requests cancellation. The task stays registered until it has actually finished.
    /// </summary>
    bool CancelTask(string taskName);
}
