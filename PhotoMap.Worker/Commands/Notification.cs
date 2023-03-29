using PhotoMap.Shared;
using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Worker.Commands
{
    public class Notification : EventBase
    {
        public IUserIdentifier UserIdentifier { get; set; }
        public string Message { get; set; }
        public bool HasError { get; set; }
        public ProcessingStatus Status { get; set; }
    }
}
