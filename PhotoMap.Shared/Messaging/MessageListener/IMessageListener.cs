namespace PhotoMap.Shared.Messaging.MessageListener;

public interface IMessageListener
{
    void Listen(CancellationToken cancellationToken);
}