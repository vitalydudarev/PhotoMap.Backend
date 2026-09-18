using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoMap.Shared.Messaging.EventHandlerManager;
using PhotoMap.Shared.Messaging.Events;
using PhotoMap.Shared.Queues;

namespace PhotoMap.Shared.Messaging.InProcess;

/// <summary>
/// Reads events published by <see cref="InProcessMessageSender"/> and passes them to the registered handlers.
/// </summary>
public class EventDispatcherBackgroundService : BackgroundService
{
    private readonly IMessageQueue<EventBase> _eventQueue;
    private readonly IEventHandlerManager _eventHandlerManager;
    private readonly ILogger<EventDispatcherBackgroundService> _logger;

    public EventDispatcherBackgroundService(
        IMessageQueue<EventBase> eventQueue,
        IEventHandlerManager eventHandlerManager,
        ILogger<EventDispatcherBackgroundService> logger)
    {
        _eventQueue = eventQueue;
        _eventHandlerManager = eventHandlerManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{ServiceName} is running.", nameof(EventDispatcherBackgroundService));

        try
        {
            await foreach (var @event in _eventQueue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    var eventHandler = _eventHandlerManager.GetHandler(@event);
                    if (eventHandler == null)
                    {
                        _logger.LogWarning("No handler registered for event {EventType}.", @event.GetType().Name);

                        continue;
                    }

                    await eventHandler.HandleAsync(@event, stoppingToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to handle event {EventType}.", @event.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Prevent throwing if stoppingToken was signaled
        }
    }
}
