namespace PhotoMap.Api.Database.Entities;

public class OutboxMessageEntity
{
    public long Id { get; set; }
    public required string Topic { get; set; }
    public required string Key { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>
    /// When Kafka acknowledged the message, null while it waits to be published.
    /// </summary>
    public DateTimeOffset? PublishedOn { get; set; }

    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
