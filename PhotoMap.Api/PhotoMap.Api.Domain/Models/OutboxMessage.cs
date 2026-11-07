namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// An event waiting in the database to be published to Kafka: the outbox. It is saved in the same transaction as
/// the change it reports, so the event exists exactly when the change does. The outbox relay publishes it later.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Assigned when saved. The messages are published in its order, and it goes with the message as its
    /// message-id header, so a consumer can tell a message it has handled already.
    /// </summary>
    public long Id { get; set; }

    public required string Topic { get; set; }

    /// <summary>
    /// The messages of one key go to one partition of the topic, in order.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The event, as JSON.
    /// </summary>
    public required string Payload { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>
    /// How many times publishing the message has failed.
    /// </summary>
    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
