using PhotoMap.Shared.Messaging.Events;
using IUserIdentifier = PhotoMap.Shared.IUserIdentifier;

namespace PhotoMap.Api.Commands
{
    public class PauseProcessingEvent : EventBase
    {
        public IUserIdentifier UserIdentifier { get; set; }
    }
}
