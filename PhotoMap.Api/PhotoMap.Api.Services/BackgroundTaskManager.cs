using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

public class BackgroundTaskManager : IBackgroundTaskManager
{
    private readonly ILogger<BackgroundTaskManager> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tasks = new();

    public BackgroundTaskManager(ILogger<BackgroundTaskManager> logger)
    {
        _logger = logger;
    }
    
    public void AddTask(string taskName, Func<Task> taskFunction, CancellationTokenSource cancellationTokenSource)
    {
        Task.Run(taskFunction, cancellationTokenSource.Token);
        
        _tasks.TryAdd(taskName, cancellationTokenSource);
    }

    public bool CancelTask(string taskName)
    {
        if (_tasks.TryGetValue(taskName, out CancellationTokenSource? cancellationTokenSource))
        {
            cancellationTokenSource.Cancel();
            _tasks.TryRemove(taskName, out _);
            
            _logger.LogInformation("Task {TaskName} cancelled", taskName);
            return true;
        }
        
        return false;
    }
}