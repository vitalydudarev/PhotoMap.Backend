using PhotoMap.Shared;
using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Worker.Commands
{
    public class ProgressMessage : EventBase
    {
        public IUserIdentifier UserIdentifier { get; set; }
        public int Processed { get; set; }
        public int Total { get; set; }
    }
}
