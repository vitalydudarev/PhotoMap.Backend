using System.Text.Json;

namespace PhotoMap.Shared.Messaging;

/// <summary>
/// Sends the messages to a Kafka topic, as JSON.
/// </summary>
public class KafkaMessageSender<T> : IMessageSender<T>
{
    private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();

    private readonly IMessagePublisher _publisher;
    private readonly string _topic;
    private readonly Func<T, string> _getKey;

    /// <param name="getKey">The key of a message: the messages of one key go to one partition, in order.</param>
    public KafkaMessageSender(IMessagePublisher publisher, string topic, Func<T, string> getKey)
    {
        _publisher = publisher;
        _topic = topic;
        _getKey = getKey;
    }

    public Task SendAsync(T message, CancellationToken cancellationToken)
    {
        return _publisher.PublishAsync(_topic, _getKey(message), JsonSerializer.Serialize(message), NoHeaders,
            cancellationToken);
    }
}
