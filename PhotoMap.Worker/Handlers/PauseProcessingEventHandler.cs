using PhotoMap.Shared.Messaging.Events;
using PhotoMap.Shared.Messaging.MessageSender;
using PhotoMap.Worker.Services.Definitions;
using Notification = PhotoMap.Worker.Commands.Notification;
using PauseProcessingEvent = PhotoMap.Worker.Commands.PauseProcessingEvent;
using ProcessingStatus = PhotoMap.Worker.Commands.ProcessingStatus;

namespace PhotoMap.Worker.Handlers
{
    public class PauseProcessingEventHandler : PhotoMap.Shared.Messaging.EventHandler.EventHandler<PauseProcessingEvent>
    {
        private readonly IMessageSender2 _messageSender;
        private readonly IDownloadManager _downloadManager;

        public PauseProcessingEventHandler(
            IMessageSender2 messageSender,
            IDownloadManager downloadManager)
        {
            _messageSender = messageSender;
            _downloadManager = downloadManager;
        }

        public override Task HandleAsync(EventBase @event, CancellationToken cancellationToken)
        {
            if (@event is PauseProcessingEvent pauseProcessingCommand)
            {
                _downloadManager.Remove(pauseProcessingCommand.UserIdentifier);

                var startedNotification = new Notification
                {
                    UserIdentifier = pauseProcessingCommand.UserIdentifier,
                    Status = ProcessingStatus.NotRunning
                };

                _messageSender.Send(startedNotification, ApiConstants.PhotoMapApi);
            }

            return Task.CompletedTask;
        }
    }
}
