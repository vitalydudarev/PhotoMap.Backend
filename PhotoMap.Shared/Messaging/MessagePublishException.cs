namespace PhotoMap.Shared.Messaging;

/// <summary>
/// A message has not been delivered to the broker, e.g. it has been down for longer than the producer waits.
/// </summary>
public class MessagePublishException : Exception
{
    public MessagePublishException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}
