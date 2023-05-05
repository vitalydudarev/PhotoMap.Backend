using System.Text;
using NATS.Client;

namespace PhotoMap.Shared.Messaging.MessageListener;

public class NatsMessageListener
{
    public void ConfigureNats(IConnection natsConnection)
    {
        natsConnection.SubscribeAsync("Subject1", (sender, args) =>
        {
            if (args.Message.Data == null)
            {
                return;
            }

            string scopesStr = Encoding.UTF8.GetString(args.Message.Data);
                
            // deserialize message
                
            // do action
               
            // reply
            // natsConnection.Publish(MessagesConstants.ResetCacheReplySubject, Encoding.UTF8.GetBytes(reply));
        });
    }
}