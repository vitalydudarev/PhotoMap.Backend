using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Worker.Commands
{
    public class ImageConverted : EventBase
    {
        public Guid Id { get; set; }

        public byte[] FileContents { get; set; }
    }
}
