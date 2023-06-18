namespace PhotoMap.Shared.Messaging.MessageSender;

public interface IMessageSenderNew
{
    Task PublishMessageAsync<T>(T message, string subject, int timeout = 30 * 1000);
}