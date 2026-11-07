using System.Globalization;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Shared.Messaging;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// How a round of the relay went.
/// </summary>
/// <param name="Read">How many messages were waiting, up to the most a round takes.</param>
/// <param name="Published">How many of them the broker has acknowledged.</param>
/// <param name="Failed">How many of them have not been delivered, they are tried again in a later round.</param>
public sealed record OutboxRelayResult(int Read, int Published, int Failed)
{
    public static OutboxRelayResult None { get; } = new(0, 0, 0);
}

/// <summary>
/// Publishes the outbox messages to the broker, and marks them published once it has acknowledged them.
/// </summary>
/// <remarks>
/// A message is marked published only after the broker has it: one the application stops in between is published
/// again on the next start, so it can reach the consumers twice, never zero times (at-least-once). The
/// <see cref="MessageHeaders.MessageId"/> header lets a consumer tell such a copy.
/// </remarks>
public class OutboxRelay
{
    private readonly IOutboxService _outboxService;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OutboxRelay> _logger;

    public OutboxRelay(IOutboxService outboxService, IMessagePublisher publisher, ILogger<OutboxRelay> logger)
    {
        _outboxService = outboxService;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<OutboxRelayResult> PublishPendingAsync(int maxCount, CancellationToken cancellationToken)
    {
        var messages = await _outboxService.GetPendingAsync(maxCount, cancellationToken);
        if (messages.Count == 0)
        {
            return OutboxRelayResult.None;
        }

        // Sent all at once, in order, rather than one after the other's acknowledgement: the producer sends them
        // together, and the idempotent producer keeps their order per partition.
        var deliveries = messages.Select(a => (Message: a, Delivery: PublishAsync(a, cancellationToken))).ToList();

        var publishedIds = new List<long>();
        var failures = new List<(long Id, string Error)>();

        foreach (var (message, delivery) in deliveries)
        {
            try
            {
                await delivery;
                publishedIds.Add(message.Id);
            }
            catch (MessagePublishException e)
            {
                failures.Add((message.Id, e.Message));
            }
        }

        // Not cancelled, the messages delivered are to be marked as such even while the application stops; the
        // ones not marked would be published again
        await _outboxService.MarkPublishedAsync(publishedIds, DateTimeOffset.UtcNow);

        foreach (var sameErrorFailures in failures.GroupBy(a => a.Error))
        {
            await _outboxService.RecordFailedAsync(sameErrorFailures.Select(a => a.Id).ToList(), sameErrorFailures.Key);
        }

        if (failures.Count > 0)
        {
            // Usually all of them fail, the broker being down. When only some do, the ones after them may have been
            // delivered, and the failed ones are delivered after those: out of the order they were saved in.
            _logger.LogWarning("Failed to publish {FailedCount} of {MessageCount} outbox messages: {Error}",
                failures.Count, messages.Count, failures[0].Error);
        }

        _logger.LogDebug("Published {PublishedCount} of {MessageCount} outbox messages", publishedIds.Count,
            messages.Count);

        return new OutboxRelayResult(messages.Count, publishedIds.Count, failures.Count);
    }

    private Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            [MessageHeaders.MessageId] = message.Id.ToString(CultureInfo.InvariantCulture)
        };

        return _publisher.PublishAsync(message.Topic, message.Key, message.Payload, headers, cancellationToken);
    }
}
