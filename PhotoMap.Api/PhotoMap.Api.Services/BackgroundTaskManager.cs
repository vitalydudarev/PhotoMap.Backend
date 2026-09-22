using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

/// <summary>
/// Runs named background tasks, at most one per name. Registered as a hosted service as well, so running tasks are
/// cancelled and awaited when the application stops.
/// </summary>
public sealed class BackgroundTaskManager : IBackgroundTaskManager, IHostedService
{
    private readonly ILogger<BackgroundTaskManager> _logger;
    private readonly ConcurrentDictionary<string, BackgroundTask> _tasks = new();

    public BackgroundTaskManager(ILogger<BackgroundTaskManager> logger)
    {
        _logger = logger;
    }

    public bool TryStartTask(string taskName, Func<CancellationToken, Task> taskFunction)
    {
        var backgroundTask = new BackgroundTask();

        // register before starting, so the same task can't be started twice
        if (!_tasks.TryAdd(taskName, backgroundTask))
        {
            return false;
        }

        backgroundTask.Task = Task.Run(async () =>
        {
            try
            {
                await taskFunction(backgroundTask.CancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Task {TaskName} failed", taskName);
            }
            finally
            {
                _tasks.TryRemove(new KeyValuePair<string, BackgroundTask>(taskName, backgroundTask));
            }
        });

        return true;
    }

    public bool IsRunning(string taskName)
    {
        return _tasks.ContainsKey(taskName);
    }

    public bool CancelTask(string taskName)
    {
        if (_tasks.TryGetValue(taskName, out var backgroundTask))
        {
            backgroundTask.CancellationTokenSource.Cancel();

            _logger.LogInformation("Task {TaskName} cancelled", taskName);
            return true;
        }
        
        return false;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var backgroundTasks = _tasks.Values.ToList();

        foreach (var backgroundTask in backgroundTasks)
        {
            backgroundTask.CancellationTokenSource.Cancel();
        }

        try
        {
            await Task.WhenAll(backgroundTasks.Select(a => a.Task)).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Background tasks didn't finish before the shutdown timeout");
        }
    }

    private sealed class BackgroundTask
    {
        public CancellationTokenSource CancellationTokenSource { get; } = new();
        public Task Task { get; set; } = Task.CompletedTask;
    }
}
