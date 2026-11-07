using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoMap.Shared.Events;
using PhotoMap.Shared.Messaging;

namespace PhotoMap.Api.Services
{
    /// <summary>
    /// Logs the photos saved, read from the <see cref="PhotoSavedEvent.Topic"/> topic. It only shows how a consumer
    /// works; what a real one would do with the events, e.g. look up the place a photo was taken, goes in
    /// <see cref="Handle"/>.
    /// </summary>
    /// <remarks>
    /// The consumers of one group share the partitions of the topic, each partition is read by one of them. Another
    /// group, e.g. of another service, reads every event again, at its own pace: Kafka keeps the events after they
    /// have been read, and each group keeps its own position (offset) in each partition.
    /// </remarks>
    public class PhotoSavedLogConsumer : BackgroundService
    {
        public const string GroupId = "photo-map.photo-saved-log";

        private readonly KafkaSettings _settings;
        private readonly ILogger<PhotoSavedLogConsumer> _logger;

        public PhotoSavedLogConsumer(KafkaSettings settings, ILogger<PhotoSavedLogConsumer> logger)
        {
            _settings = settings;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Consume blocks its thread until a message arrives, so the loop gets a thread of its own rather than
            // one of the thread pool
            return Task.Factory.StartNew(() => Consume(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private void Consume(CancellationToken stoppingToken)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                GroupId = GroupId,
                // where a group with no offset committed yet starts: from the oldest event kept, not only the new ones
                AutoOffsetReset = AutoOffsetReset.Earliest,
                // the offset is committed once the event has been handled, see below
                EnableAutoCommit = false
            };

            using var consumer = new ConsumerBuilder<string, string>(config)
                .SetErrorHandler((_, error) => _logger.LogWarning("Kafka consumer: {Reason}", error.Reason))
                .SetLogHandler((_, message) => _logger.LogDebug("Kafka consumer: {Message}", message.Message))
                // the partitions change when a consumer of the group starts or stops: a rebalance
                .SetPartitionsAssignedHandler((_, partitions) =>
                    _logger.LogInformation("Kafka consumer assigned partitions {Partitions}", partitions))
                .SetPartitionsRevokedHandler((_, partitions) =>
                    _logger.LogInformation("Kafka consumer revoked partitions {Partitions}", partitions))
                .Build();

            consumer.Subscribe(PhotoSavedEvent.Topic);

            _logger.LogInformation("{ServiceName} is consuming {Topic} as group {GroupId}", nameof(PhotoSavedLogConsumer),
                PhotoSavedEvent.Topic, GroupId);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    ConsumeResult<string, string> result;

                    try
                    {
                        result = consumer.Consume(stoppingToken);
                    }
                    catch (ConsumeException e)
                    {
                        _logger.LogError(e, "Failed to consume from {Topic}", PhotoSavedEvent.Topic);
                        continue;
                    }

                    Handle(result);

                    // Committed after handling: when the application stops in between, the event is consumed again
                    // on the next start (at-least-once). Committed before handling, it would be lost instead
                    // (at-most-once). A consumer must hence handle an event twice without harm, e.g. by keeping the
                    // message-id headers of the events it has handled.
                    try
                    {
                        consumer.Commit(result);
                    }
                    catch (KafkaException e)
                    {
                        // e.g. the partition has been given to another consumer of the group meanwhile, which
                        // consumes the event again
                        _logger.LogWarning(e, "Failed to commit offset {Offset} of {Topic}", result.TopicPartitionOffset,
                            PhotoSavedEvent.Topic);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // the application is stopping
            }
            finally
            {
                // leaves the group right away, so that its partitions go to the other consumers of the group now,
                // not once the broker has stopped hearing from this one
                consumer.Close();
            }
        }

        private void Handle(ConsumeResult<string, string> result)
        {
            PhotoSavedEvent? photoSaved;

            try
            {
                photoSaved = JsonSerializer.Deserialize<PhotoSavedEvent>(result.Message.Value);
            }
            catch (JsonException e)
            {
                // a malformed event is skipped, rather than retried forever, holding up the partition: a poison
                // message. A real consumer would write it to a dead-letter topic to look into.
                _logger.LogWarning(e, "Skipping malformed event at {Offset}", result.TopicPartitionOffset);
                return;
            }

            if (photoSaved == null)
            {
                _logger.LogWarning("Skipping empty event at {Offset}", result.TopicPartitionOffset);
                return;
            }

            _logger.LogInformation(
                "Photo saved: {FileName} of user {UserId}, {PhotoSourceName}, taken on {DateTimeTaken}, {Location} " +
                "(message {MessageId}, key {Key}, partition {Partition}, offset {Offset})",
                photoSaved.FileName, photoSaved.UserId, photoSaved.PhotoSourceName, photoSaved.DateTimeTaken,
                photoSaved.Latitude.HasValue ? $"at {photoSaved.Latitude}, {photoSaved.Longitude}" : "no location",
                GetMessageId(result.Message), result.Message.Key, result.Partition.Value, result.Offset.Value);
        }

        /// <summary>
        /// The id of the outbox message the event was published from; the same id seen twice is the same event
        /// delivered again.
        /// </summary>
        private static string? GetMessageId(Message<string, string> message)
        {
            return message.Headers != null && message.Headers.TryGetLastBytes(MessageHeaders.MessageId, out var bytes)
                ? Encoding.UTF8.GetString(bytes)
                : null;
        }
    }
}
