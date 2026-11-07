using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace PhotoMap.Shared.Messaging;

/// <summary>
/// Receives the messages of a Kafka topic, as JSON, as a member of a consumer group.
/// </summary>
/// <remarks>
/// A Kafka consumer is not thread-safe; the calls to it, made one at a time by the one consumer of the messages,
/// may each come from another thread, which it allows.
/// </remarks>
public sealed class KafkaMessageReceiver<T> : IMessageReceiver<T>, IDisposable
{
    private readonly IConsumer<string, string> _consumer;
    private readonly string _topic;
    private readonly ILogger _logger;

    public KafkaMessageReceiver(KafkaSettings settings, string topic, string groupId, ILogger logger)
    {
        _topic = topic;
        _logger = logger;

        var config = KafkaClients.Configure(new ConsumerConfig
        {
            GroupId = groupId,
            // a group with no offset committed yet starts from the oldest message kept: none is skipped
            AutoOffsetReset = AutoOffsetReset.Earliest,
            // committed by Acknowledge, once the messages have been handled
            EnableAutoCommit = false
        }, settings);

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => KafkaClients.LogError(_logger, $"consumer of {_topic}", error))
            .SetLogHandler((_, message) => _logger.LogDebug("Kafka consumer of {Topic}: {Message}", _topic, message.Message))
            .SetPartitionsAssignedHandler((_, partitions) =>
                _logger.LogInformation("Kafka consumer of {Topic} assigned partitions {Partitions}", _topic, partitions))
            .SetPartitionsRevokedHandler((_, partitions) =>
                _logger.LogInformation("Kafka consumer of {Topic} revoked partitions {Partitions}", _topic, partitions))
            .Build();

        _consumer.Subscribe(topic);
    }

    public Task<IReadOnlyList<T>> ReceiveBatchAsync(int maxCount, TimeSpan maxWait, CancellationToken cancellationToken)
    {
        // Consume blocks its thread while it waits for messages
        return Task.Run(() => ReceiveBatch(maxCount, maxWait, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Commits the offsets of the messages received so far: a consumer of the group, this one after a restart or
    /// another one the partitions have gone to, starts after them.
    /// </summary>
    public void Acknowledge()
    {
        try
        {
            _consumer.Commit();
        }
        catch (KafkaException e)
        {
            // e.g. the partitions have gone to another consumer of the group meanwhile, which receives the messages
            // again; or there is no offset to commit, all the messages received having been skipped
            _logger.LogWarning("Failed to commit the offsets of {Topic}: {Reason}", _topic, e.Error.Reason);
        }
    }

    public void Dispose()
    {
        // leaves the group right away, so that its partitions go to the other consumers of the group now
        _consumer.Close();
        _consumer.Dispose();
    }

    private IReadOnlyList<T> ReceiveBatch(int maxCount, TimeSpan maxWait, CancellationToken cancellationToken)
    {
        var messages = new List<T>();

        // until a message to hand over, however long: a batch of none would tell that no more will come
        while (messages.Count == 0)
        {
            TryAdd(messages, Consume(cancellationToken));
        }

        var deadline = DateTime.UtcNow + maxWait;

        while (messages.Count < maxCount)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var result = _consumer.Consume(remaining);
            if (result == null)
            {
                // the time to wait for more is up
                break;
            }

            TryAdd(messages, result);
        }

        return messages;
    }

    private ConsumeResult<string, string>? Consume(CancellationToken cancellationToken)
    {
        try
        {
            return _consumer.Consume(cancellationToken);
        }
        catch (ConsumeException e)
        {
            _logger.LogError(e, "Failed to consume from {Topic}", _topic);
            return null;
        }
    }

    private void TryAdd(List<T> messages, ConsumeResult<string, string>? result)
    {
        if (result == null)
        {
            return;
        }

        try
        {
            var message = JsonSerializer.Deserialize<T>(result.Message.Value);
            if (message != null)
            {
                messages.Add(message);
                return;
            }
        }
        catch (JsonException e)
        {
            _logger.LogWarning(e, "Skipping malformed message at {Offset}", result.TopicPartitionOffset);
            return;
        }

        _logger.LogWarning("Skipping empty message at {Offset}", result.TopicPartitionOffset);
    }
}
