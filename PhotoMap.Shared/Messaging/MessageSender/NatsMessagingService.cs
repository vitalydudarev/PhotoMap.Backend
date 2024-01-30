using System.Text;
using NATS.Client;

namespace PhotoMap.Shared.Messaging.MessageSender;

public class NatsMessagingService : IMessagingService
{
    private readonly string _natsUrl;

    public NatsMessagingService(string natsUrl)
    {
        if (string.IsNullOrEmpty(natsUrl))
        {
            throw new Exception("NATS url is not correct.");
        }
        
        _natsUrl = natsUrl;
    }
    
    public async Task PublishMessageAsync<T>(string subject, T message, int timeout = 30 * 1000)
    {
        Msg msg = new()
        {
            Subject = subject,
            Data = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(message))
        };

        using IConnection connection = new ConnectionFactory().CreateConnection($"nats://{_natsUrl}");
        
        try
        {
            connection.Publish(msg);

            await connection.DrainAsync();
        }
        catch (NATSTimeoutException)
        {
            //
        }
    }
}