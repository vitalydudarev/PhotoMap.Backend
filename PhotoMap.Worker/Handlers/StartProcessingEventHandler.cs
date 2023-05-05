using PhotoMap.Shared;
using PhotoMap.Shared.Messaging.Events;
using PhotoMap.Shared.Messaging.MessageSender;
using PhotoMap.Worker.Models;
using PhotoMap.Worker.Services.Definitions;
using DropboxUserIdentifier = PhotoMap.Worker.Models.DropboxUserIdentifier;
using ImageProcessedEvent = PhotoMap.Shared.Events.ImageProcessedEvent;
using Notification = PhotoMap.Worker.Commands.Notification;
using ProcessingStatus = PhotoMap.Worker.Commands.ProcessingStatus;
using StartProcessingEvent = PhotoMap.Worker.Commands.StartProcessingEvent;
using YandexDiskUserIdentifier = PhotoMap.Worker.Models.YandexDiskUserIdentifier;

namespace PhotoMap.Worker.Handlers
{
    public class StartProcessingEventHandler : PhotoMap.Shared.Messaging.EventHandler.EventHandler<StartProcessingEvent>
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<StartProcessingEventHandler> _logger;
        private readonly IMessageSender2 _messageSender;
        private readonly IDownloadManager _downloadManager;
        private readonly IImageProcessingService _imageProcessingService;

        public StartProcessingEventHandler(
            IServiceScopeFactory serviceScopeFactory,
            IMessageSender2 messageSender,
            IDownloadManager downloadManager,
            IImageProcessingService imageProcessingService,
            ILogger<StartProcessingEventHandler> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _messageSender = messageSender;
            _downloadManager = downloadManager;
            _imageProcessingService = imageProcessingService;
            _logger = logger;
        }

        public override async Task HandleAsync(EventBase @event, CancellationToken cancellationToken)
        {
            if (@event is StartProcessingEvent startProcessingCommand)
            {
                using var scope = _serviceScopeFactory.CreateScope();

                var userIdentifier = startProcessingCommand.UserIdentifier;

                var stoppingAction = new StoppingAction();
                _downloadManager.Add(userIdentifier, stoppingAction);

                var startedNotification = CreateNotification(userIdentifier, ProcessingStatus.Running);
                _messageSender.Send(startedNotification, ApiConstants.PhotoMapApi);

                if (userIdentifier is YandexDiskUserIdentifier)
                {
                    var yandexDiskDownloadService = scope.ServiceProvider.GetRequiredService<IYandexDiskDownloadService>();

                    await HandleYandexRequestAsync(yandexDiskDownloadService, userIdentifier, startProcessingCommand,
                        stoppingAction, cancellationToken);
                }
                else if (userIdentifier is DropboxUserIdentifier)
                {
                    var dropboxDownloadService = scope.ServiceProvider.GetRequiredService<IDropboxDownloadService>();

                    await HandleDropboxRequestAsync(dropboxDownloadService, userIdentifier, startProcessingCommand,
                        stoppingAction, cancellationToken);
                }
            }
        }

        private async Task HandleYandexRequestAsync(
            IYandexDiskDownloadService yandexDiskDownloadService,
            IUserIdentifier userIdentifier,
            StartProcessingEvent startProcessingCommand,
            StoppingAction stoppingAction,
            CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var file in yandexDiskDownloadService.DownloadFilesAsync(userIdentifier,
                                   startProcessingCommand.Token, cancellationToken, stoppingAction))
                {
                    var processedDownloadedFile = await _imageProcessingService.ProcessImageAsync(file);
                    var imageProcessedEvent = CreateImageProcessedEvent(startProcessingCommand.UserIdentifier, processedDownloadedFile);
                    _messageSender.Send(imageProcessedEvent, ApiConstants.PhotoMapApi);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);

                var stoppedNotification =
                    CreateNotification(userIdentifier, ProcessingStatus.NotRunning, true, e.Message);
                _messageSender.Send(stoppedNotification, ApiConstants.PhotoMapApi);
            }
            finally
            {
                _downloadManager.Remove(userIdentifier);

                var finishedNotification = CreateNotification(userIdentifier, ProcessingStatus.NotRunning);
                _messageSender.Send(finishedNotification, ApiConstants.PhotoMapApi);

                _logger.LogInformation("Processing finished");
            }
        }

        private async Task HandleDropboxRequestAsync(
            IDropboxDownloadService dropboxDownloadService,
            IUserIdentifier userIdentifier,
            StartProcessingEvent startProcessingCommand,
            StoppingAction stoppingAction,
            CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var file in dropboxDownloadService.DownloadAsync(userIdentifier,
                                   startProcessingCommand.Token, stoppingAction, cancellationToken))
                {
                    var processedDownloadedFile = await _imageProcessingService.ProcessImageAsync(file);
                    var imageProcessedEvent = CreateImageProcessedEvent(startProcessingCommand.UserIdentifier, processedDownloadedFile);
                    _messageSender.Send(imageProcessedEvent, ApiConstants.PhotoMapApi);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);

                var stoppedNotification =
                    CreateNotification(userIdentifier, ProcessingStatus.NotRunning, true, e.Message);
                _messageSender.Send(stoppedNotification, ApiConstants.PhotoMapApi);
            }
            finally
            {
                _downloadManager.Remove(userIdentifier);

                var finishedNotification = CreateNotification(userIdentifier, ProcessingStatus.NotRunning);
                _messageSender.Send(finishedNotification, ApiConstants.PhotoMapApi);

                _logger.LogInformation("Processing finished.");
            }
        }

        private static ImageProcessedEvent CreateImageProcessedEvent(IUserIdentifier userIdentifier, ProcessedDownloadedFile file)
        {
            return new ImageProcessedEvent
            {
                UserIdentifier = userIdentifier,
                FileName = file.FileName,
                PhotoSourceId = file.PhotoSourceId,
                FileSource = file.FileSource,
                Thumbs = file.Thumbs,
                Path = file.Path,
                FileCreatedOn = file.FileCreatedOn,
                PhotoTakenOn = file.PhotoTakenOn,
                ExifString = file.ExifString,
                Latitude = file.Latitude,
                Longitude = file.Longitude
            };
        }

        private static Notification CreateNotification(IUserIdentifier userIdentifier, ProcessingStatus status,
            bool hasError = false, string? message = null)
        {
            return new Notification
            {
                UserIdentifier = userIdentifier,
                Status = status,
                HasError = hasError,
                Message = message
            };
        }
    }
}
