using PhotoMap.Shared;
using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Worker.Commands
{
    public class StartProcessingEvent : EventBase
    {
        public IUserIdentifier UserIdentifier { get; set; }
        public string Token { get; set; }
    }
}
