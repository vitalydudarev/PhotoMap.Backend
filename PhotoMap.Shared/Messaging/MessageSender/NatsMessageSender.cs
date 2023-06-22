using System.Text;
using NATS.Client;

namespace PhotoMap.Shared.Messaging.MessageSender;

public class NatsMessageSender : IMessageSenderNew
{
    private readonly string _natsUrl;

    public NatsMessageSender(string natsUrl)
    {
        if (string.IsNullOrEmpty(natsUrl))
        {
            throw new Exception("NATS url is not correct.");
        }
        
        _natsUrl = natsUrl;
    }
    
    public async Task PublishMessageAsync<T>(T message, string subject, int timeout = 30 * 1000)
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