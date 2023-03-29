using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Services.Interfaces;
using PhotoMap.Shared.Events;
using PhotoMap.Shared.Messaging.EventHandler;
using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Api.Handlers
{
    public class ImageConvertedHandler : EventHandler<ImageConvertedEvent>
    {
        private readonly ILogger<ImageConvertedHandler> _logger;
        private readonly IConvertedImageHolder _convertedImageHolder;

        public ImageConvertedHandler(
            IConvertedImageHolder convertedImageHolder,
            ILogger<ImageConvertedHandler> logger)
        {
            _convertedImageHolder = convertedImageHolder;
            _logger = logger;
        }

        public override Task HandleAsync(EventBase @event, CancellationToken cancellationToken)
        {
            if (@event is ImageConvertedEvent imageConverted)
            {
                _convertedImageHolder.Add(imageConverted.Id, imageConverted.FileContents);
                _logger.LogInformation("Converted image for {Id} received", imageConverted.Id);
            }

            return Task.CompletedTask;
        }
    }
}
