using PhotoMap.Shared.Messaging.Events;
using IUserIdentifier = PhotoMap.Shared.IUserIdentifier;

namespace PhotoMap.Api.Commands
{
    public class StartProcessingEvent : EventBase
    {
        public IUserIdentifier UserIdentifier { get; set; }

        public string Token { get; set; }
    }
}
