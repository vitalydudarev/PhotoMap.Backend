using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NATS.Client;

namespace PhotoMap.Shared.Messaging.MessageSender;

public class NatsMessagingService : IMessagingService
{
    private readonly ILogger<NatsMessagingService> _logger;
    private readonly IConnection _connection;

    public NatsMessagingService(ILogger<NatsMessagingService> logger, IConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }
    
    public async Task PublishMessageAsync<T>(string subject, T message, int timeout = 30 * 1000)
    {
        Msg msg = new()
        {
            Subject = subject,
            Data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message))
        };

        try
        {
            _connection.Publish(msg);

            await _connection.DrainAsync();
        }
        catch (NATSTimeoutException e)
        {
            _logger.LogError(e, "Failed to publish message because of timeout: {ErrorMessage}", e.Message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to publish message: {ErrorMessage}", e.Message);
        }
    }
}