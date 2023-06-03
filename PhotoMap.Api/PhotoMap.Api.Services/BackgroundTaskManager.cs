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
    
    public void Run(string taskName, Func<Task> taskFunction, CancellationTokenSource cancellationTokenSource)
    {
        Task.Run(async () =>
        {
            await Task.Run(taskFunction);
        });

        // try this
        // Task.Run(taskFunction, cancellationTokenSource.Token);
        
        _tasks.TryAdd(taskName, cancellationTokenSource);
    }

    public bool Cancel(string taskName)
    {
        if (_tasks.TryGetValue(taskName, out CancellationTokenSource? cancellationTokenSource))
        {
            cancellationTokenSource.Cancel();
            _tasks.TryRemove(taskName, out _);
            
            _logger.LogInformation("BackgroundTaskManager {taskName} cancelled", taskName);
            return true;
        }
        
        return false;
    }
}