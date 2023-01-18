using System;
using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Api.Commands
{
    public class ConvertImageEvent : EventBase
    {
        public Guid Id { get; set; }

        public byte[] FileContents { get; set; }
    }
}
