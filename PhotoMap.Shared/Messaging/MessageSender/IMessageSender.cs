using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Shared.Messaging.MessageSender;

public interface IMessageSender
{
    void Send(EventBase eventBase);
}