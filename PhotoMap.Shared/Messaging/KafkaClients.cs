using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace PhotoMap.Shared.Messaging;

/// <summary>
/// What every Kafka client of the application, producer, consumer or admin, is configured and logs with.
/// </summary>
public static class KafkaClients
{
    /// <summary>
    /// How long a client waits before connecting to a broker again, doubling up to
    /// <see cref="ReconnectBackoffMax"/>. The default, 100 ms, has every client try ten times a second while Kafka is
    /// down, for nothing.
    /// </summary>
    private static readonly TimeSpan ReconnectBackoff = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan ReconnectBackoffMax = TimeSpan.FromSeconds(10);

    public static T Configure<T>(T config, KafkaSettings settings) where T : ClientConfig
    {
        config.BootstrapServers = settings.BootstrapServers;
        config.ReconnectBackoffMs = (int)ReconnectBackoff.TotalMilliseconds;
        config.ReconnectBackoffMaxMs = (int)ReconnectBackoffMax.TotalMilliseconds;

        return config;
    }

    /// <summary>
    /// For the error handler of a client. Most errors are of the connection, e.g. the broker being down, which the
    /// client recovers from on its own, reconnecting; they come with every attempt, so they are logged at Debug, and
    /// the API's monitor of the availability says once that Kafka cannot be reached. A fatal error, after which the
    /// client cannot be used any longer, is logged as an error.
    /// </summary>
    public static void LogError(ILogger logger, string client, Error error)
    {
        if (error.IsFatal)
        {
            logger.LogError("Kafka {Client}: fatal error {Code}: {Reason}", client, error.Code, error.Reason);
        }
        else
        {
            logger.LogDebug("Kafka {Client}: {Reason}", client, error.Reason);
        }
    }
}
