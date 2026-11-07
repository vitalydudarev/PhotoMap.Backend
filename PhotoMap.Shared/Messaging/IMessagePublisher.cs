namespace PhotoMap.Shared.Messaging;

/// <summary>
/// Publishes messages to a broker, for whoever is interested in them: the publisher does not know the consumers.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Completes once the broker has acknowledged the message.
    /// </summary>
    /// <param name="topic">The topic the message is written to.</param>
    /// <param name="key">The messages of one key go to one partition, so they are consumed in the order published.</param>
    /// <param name="payload">The message, e.g. an event as JSON.</param>
    /// <param name="headers">Metadata that goes with the message, e.g. <see cref="MessageHeaders.MessageId"/>.</param>
    /// <exception cref="MessagePublishException">The message has not been delivered.</exception>
    Task PublishAsync(string topic, string key, string payload, IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken);
}
