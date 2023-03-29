using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Shared.Messaging.EventHandler;

public abstract class EventHandler<T> : IEventHandler where T : EventBase
{
    public Type Type => typeof(T);

    public abstract Task HandleAsync(EventBase @event, CancellationToken cancellationToken);
}

public interface IEventHandler2<in T> where T : EventBase
{
    Type Type { get; }

    Task HandleAsync(T @event, CancellationToken cancellationToken);
}

public abstract class EventHandler2<T> : IEventHandler2<T> where T : EventBase
{
    public Type Type => typeof(T);

    public abstract Task HandleAsync(T @event, CancellationToken cancellationToken);
}