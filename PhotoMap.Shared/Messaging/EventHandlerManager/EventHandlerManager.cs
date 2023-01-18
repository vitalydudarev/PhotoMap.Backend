using PhotoMap.Shared.Messaging.EventHandler;
using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Shared.Messaging.EventHandlerManager;

public class EventHandlerManager : IEventHandlerManager
{
    private readonly Dictionary<Type, IEventHandler> _eventHandlerMap;

    public EventHandlerManager(IEnumerable<IEventHandler> eventHandlers)
    {
        _eventHandlerMap = eventHandlers.ToDictionary(a => a.Type, b => b);
    }

    public IEventHandler GetHandler(EventBase eventBase)
    {
        var commandType = eventBase.GetType();

        return _eventHandlerMap.TryGetValue(commandType, out var commandHandler) ? commandHandler : null;
    }
}