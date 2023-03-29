using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Shared.Events;

public class ConvertImageEvent : EventBase
{
    public Guid Id { get; set; }

    public byte[] FileContents { get; set; }
}