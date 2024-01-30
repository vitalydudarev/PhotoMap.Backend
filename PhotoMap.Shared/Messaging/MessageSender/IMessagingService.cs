namespace PhotoMap.Shared.Messaging.MessageSender;

public interface IMessagingService
{
    Task PublishMessageAsync<T>(string subject, T message, int timeout = 30 * 1000);
}