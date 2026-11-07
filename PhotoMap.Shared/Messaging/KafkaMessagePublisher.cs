using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace PhotoMap.Shared.Messaging;

/// <summary>
/// One producer for the whole application: it is thread-safe, and it batches the messages of every caller into
/// fewer requests to the broker.
/// </summary>
public sealed class KafkaMessagePublisher : IMessagePublisher, IDisposable
{
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(10);

    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaMessagePublisher> _logger;

    public KafkaMessagePublisher(KafkaSettings settings, ILogger<KafkaMessagePublisher> logger)
    {
        _logger = logger;

        var config = KafkaClients.Configure(new ProducerConfig
        {
            // the broker acknowledges a message once every in-sync replica has it, not only the partition leader
            Acks = Acks.All,
            // a message retried after a lost acknowledgement is not written twice, nor out of order
            EnableIdempotence = true,
            // how long PublishAsync waits for the broker, retries included, before the message fails
            MessageTimeoutMs = (int)settings.MessageTimeout.TotalMilliseconds
        }, settings);

        _producer = new ProducerBuilder<string, string>(config)
            // errors of the connection, e.g. the broker being down; the client reconnects on its own
            .SetErrorHandler((_, error) => KafkaClients.LogError(_logger, "producer", error))
            // librdkafka writes its own log to stderr otherwise
            .SetLogHandler((_, message) => _logger.LogDebug("Kafka producer: {Message}", message.Message))
            .Build();
    }

    public async Task PublishAsync(string topic, string key, string payload, IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var message = new Message<string, string> { Key = key, Value = payload, Headers = new Headers() };

        foreach (var (name, value) in headers)
        {
            message.Headers.Add(name, Encoding.UTF8.GetBytes(value));
        }

        try
        {
            // completes once the broker has acknowledged the message. The messages of calls made one after another,
            // without waiting, are sent together and keep their order per partition.
            await _producer.ProduceAsync(topic, message, cancellationToken);
        }
        catch (KafkaException e)
        {
            // a ProduceException when the broker has not acknowledged it in time, or e.g. the queue of the producer
            // being full
            throw new MessagePublishException($"Failed to deliver the message to {topic}: {e.Error.Reason}", e);
        }
    }

    /// <summary>
    /// Waits for the messages being sent to be delivered.
    /// </summary>
    public void Dispose()
    {
        var undelivered = _producer.Flush(FlushTimeout);
        if (undelivered > 0)
        {
            _logger.LogWarning("{Count} Kafka messages have not been delivered before stopping", undelivered);
        }

        _producer.Dispose();
    }
}
