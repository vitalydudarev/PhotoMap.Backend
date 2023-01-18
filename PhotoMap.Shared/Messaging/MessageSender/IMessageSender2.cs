using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Shared.Messaging.MessageSender;

public interface IMessageSender2
{
    void Send(EventBase eventBase, string consumerApi);
}