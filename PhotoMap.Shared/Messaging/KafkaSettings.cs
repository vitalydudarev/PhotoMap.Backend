namespace PhotoMap.Shared.Messaging;

public class KafkaSettings
{
    /// <summary>
    /// Events are published to Kafka and consumed from it; when false, they are not published at all.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The brokers a client first connects to, it learns about the others of the cluster from them.
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// How long a message waits to be delivered, retries included, before the producer gives it up. The messages
    /// waiting are kept in memory, so while the broker is down they pile up until then.
    /// </summary>
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
