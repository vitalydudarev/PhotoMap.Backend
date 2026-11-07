namespace PhotoMap.Shared.Messaging;

/// <summary>
/// The headers of the messages, shared by the publishers and the consumers.
/// </summary>
public static class MessageHeaders
{
    /// <summary>
    /// Tells a message from the others, the same each time it is published again. Kafka delivers a message at least
    /// once, so a consumer can be handed it twice; one that must not handle it twice keeps the ids it has handled.
    /// </summary>
    public const string MessageId = "message-id";
}
