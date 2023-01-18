using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Shared.Messaging.EventHandler;

public interface IEventHandler
{
    Type Type { get; }

    Task HandleAsync(EventBase @event, CancellationToken cancellationToken);
}