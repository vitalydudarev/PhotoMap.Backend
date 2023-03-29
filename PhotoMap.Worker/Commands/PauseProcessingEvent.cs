using PhotoMap.Shared;
using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Worker.Commands
{
    public class PauseProcessingEvent : EventBase
    {
        public IUserIdentifier UserIdentifier { get; set; }
    }
}
