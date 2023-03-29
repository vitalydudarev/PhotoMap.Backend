using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Shared.Events;

public class ImageConvertedEvent : EventBase
{
    public Guid Id { get; set; }

    public byte[] FileContents { get; set; }
}