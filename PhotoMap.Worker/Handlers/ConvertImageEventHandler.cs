using PhotoMap.Shared.Events;
using PhotoMap.Shared.Messaging.Events;
using PhotoMap.Shared.Messaging.MessageSender;
using PhotoMap.Worker.Services.Implementations;
using PhotoMap.Worker.Services.Implementations.Core;

namespace PhotoMap.Worker.Handlers
{
    public class ConvertImageEventHandler : PhotoMap.Shared.Messaging.EventHandler.EventHandler<ConvertImageEvent>
    {
        private readonly ILogger<ConvertImageEventHandler> _logger;
        private readonly IMessageSender2 _messageSender;

        public ConvertImageEventHandler(
            IMessageSender2 messageSender,
            ILogger<ConvertImageEventHandler> logger)
        {
            _messageSender = messageSender;
            _logger = logger;
        }

        public override Task HandleAsync(EventBase @event, CancellationToken cancellationToken)
        {
            if (@event is ConvertImageEvent convertImageCommand)
            {
                var imageProcessor = new ImageProcessor(convertImageCommand.FileContents);
                var convertImageBytes = imageProcessor.GetImageBytes();

                var imageConverted = new ImageConvertedEvent
                {
                    Id = convertImageCommand.Id,
                    FileContents = convertImageBytes
                };

                _logger.LogInformation("Image for {Id} converted", convertImageCommand.Id);
                _messageSender.Send(imageConverted, ApiConstants.PhotoMapApi);
            }

            return Task.CompletedTask;
        }
    }
}
