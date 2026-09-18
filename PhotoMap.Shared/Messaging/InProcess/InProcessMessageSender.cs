using PhotoMap.Shared.Messaging.Events;
using PhotoMap.Shared.Messaging.MessageSender;
using PhotoMap.Shared.Queues;

namespace PhotoMap.Shared.Messaging.InProcess;

/// <summary>
/// Publishes events into an in-process queue instead of an external broker.
/// Since producers and consumers live in the same process, the consumer API is ignored.
/// </summary>
public class InProcessMessageSender : IMessageSender, IMessageSender2
{
    private readonly IMessageQueue<EventBase> _eventQueue;

    public InProcessMessageSender(IMessageQueue<EventBase> eventQueue)
    {
        _eventQueue = eventQueue;
    }

    public void Send(EventBase eventBase)
    {
        _eventQueue.EnqueueAsync(eventBase).AsTask().GetAwaiter().GetResult();
    }

    public void Send(EventBase eventBase, string consumerApi)
    {
        Send(eventBase);
    }
}
