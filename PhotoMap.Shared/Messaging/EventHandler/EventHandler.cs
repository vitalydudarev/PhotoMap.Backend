using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Shared.Messaging.EventHandler;

public abstract class EventHandler<T> : IEventHandler where T : EventBase
{
    public Type Type => typeof(T);

    public abstract Task HandleAsync(EventBase @event, CancellationToken cancellationToken);
}