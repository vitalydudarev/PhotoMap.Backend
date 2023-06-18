using System.Text;
using NATS.Client;

namespace PhotoMap.Shared.Messaging.MessageSender;

public class NatsMessageSender : IMessageSenderNew
{
    private readonly string _natsUrl;
    private readonly bool _hasNats;

    public NatsMessageSender(string natsUrl)
    {
        _natsUrl = natsUrl;
        _hasNats = !string.IsNullOrEmpty(_natsUrl);
    }
    
    public async Task PublishMessageAsync<T>(T message, string subject, int timeout = 30 * 1000)
    {
        if (!_hasNats)
        {
            return;
        }

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