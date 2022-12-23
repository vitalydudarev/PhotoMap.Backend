using PhotoMap.Messaging.Events;
using PhotoMap.Messaging.MessageSender;
using PhotoMap.Worker.Services.Implementations;
using ConvertImageEvent = PhotoMap.Worker.Commands.ConvertImageEvent;
using ImageConverted = PhotoMap.Worker.Commands.ImageConverted;

namespace PhotoMap.Worker.Handlers
{
    public class ConvertImageEventHandler : Messaging.EventHandler.EventHandler<ConvertImageEvent>
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

                var imageConverted = new ImageConverted
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
