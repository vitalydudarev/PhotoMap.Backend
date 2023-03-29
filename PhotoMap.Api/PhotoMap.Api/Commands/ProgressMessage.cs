using PhotoMap.Shared.Messaging.Events;
using IUserIdentifier = PhotoMap.Shared.IUserIdentifier;

namespace PhotoMap.Api.Commands
{
    public class ProgressMessage : EventBase
    {
        public IUserIdentifier UserIdentifier { get; set; }

        public int Processed { get; set; }

        public int Total { get; set; }
    }
}
