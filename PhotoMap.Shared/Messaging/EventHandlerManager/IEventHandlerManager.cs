using PhotoMap.Shared.Messaging.EventHandler;
using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Shared.Messaging.EventHandlerManager;

public interface IEventHandlerManager
{
    IEventHandler? GetHandler(EventBase eventBase);
}