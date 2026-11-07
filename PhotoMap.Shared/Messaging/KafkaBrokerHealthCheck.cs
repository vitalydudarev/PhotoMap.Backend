using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace PhotoMap.Shared.Messaging;

/// <summary>
/// Tells whether the Kafka cluster can be reached, by asking a broker for the metadata of the cluster.
/// </summary>
public interface IBrokerHealthCheck
{
    /// <returns>Whether a broker has answered within <paramref name="timeout"/>.</returns>
    bool IsReachable(TimeSpan timeout);
}

/// <summary>
/// One admin client, kept: it reconnects on its own, and a check is then one request to a broker it is connected to.
/// </summary>
public sealed class KafkaBrokerHealthCheck : IBrokerHealthCheck, IDisposable
{
    private readonly IAdminClient _adminClient;
    private readonly ILogger<KafkaBrokerHealthCheck> _logger;

    public KafkaBrokerHealthCheck(KafkaSettings settings, ILogger<KafkaBrokerHealthCheck> logger)
    {
        _logger = logger;
        _adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = settings.BootstrapServers })
            // the monitor of the availability logs when the broker goes and comes back, not every failed connection
            .SetErrorHandler((_, error) => _logger.LogDebug("Kafka admin client: {Reason}", error.Reason))
            .SetLogHandler((_, message) => _logger.LogDebug("Kafka admin client: {Message}", message.Message))
            .Build();
    }

    public bool IsReachable(TimeSpan timeout)
    {
        try
        {
            // a request to a broker: it fails, rather than answering from what the client knows already, when no
            // broker answers in time
            var metadata = _adminClient.GetMetadata(timeout);

            return metadata.Brokers.Count > 0;
        }
        catch (KafkaException e)
        {
            _logger.LogDebug("Kafka cannot be reached: {Reason}", e.Error.Reason);
            return false;
        }
    }

    public void Dispose()
    {
        _adminClient.Dispose();
    }
}
