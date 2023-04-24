using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Interfaces;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Events;
using PhotoMap.Shared.Messaging.Events;

namespace PhotoMap.Api.Handlers
{
    public class ImageProcessedEventHandler : PhotoMap.Shared.Messaging.EventHandler.EventHandler<ImageProcessedEvent>
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ImageProcessedEventHandler> _logger;

        public ImageProcessedEventHandler(IServiceScopeFactory serviceScopeFactory, ILogger<ImageProcessedEventHandler> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public override async Task HandleAsync(EventBase @event, CancellationToken cancellationToken)
        {
            if (@event is ImageProcessedEvent imageProcessedEvent)
            {
                var scope = _serviceScopeFactory.CreateScope();
                var photoService = scope.ServiceProvider.GetRequiredService<IPhotoService>();
                var imageStore = scope.ServiceProvider.GetRequiredService<IImageStore>();

                var thumbs = imageProcessedEvent.Thumbs.OrderBy(a => a.Key).ToDictionary(a => a.Key, b => b.Value);
                var thumbSmall = thumbs.FirstOrDefault();
                var thumbLarge = thumbs.LastOrDefault();

                var userName = imageProcessedEvent.UserIdentifier.GetKey();
                
                var thumbSmallPath = await imageStore.SaveThumbnailAsync(thumbSmall.Value, imageProcessedEvent.FileName,
                    userName, imageProcessedEvent.FileSource, thumbSmall.Key);
                
                var thumbLargePath = await imageStore.SaveThumbnailAsync(thumbLarge.Value, imageProcessedEvent.FileName,
                    userName, imageProcessedEvent.FileSource, thumbLarge.Key);

                // var entity = await photoService.GetByFileNameAsync(imageProcessedEvent.FileName);
                // if (entity != null)
                // {
                    // await storageService.DeleteFileAsync(thumbSmall);
                    // await storageService.DeleteFileAsync(thumbLarge);

                    // _logger.LogInformation("File {FileName} already exists", imageProcessedEvent.FileName);

                    // return;
                // }
                
                // TODO: save thumbs on disk and then add the paths of the files to photoEntity

                var photoEntity = new Photo
                {
                    UserId = imageProcessedEvent.UserIdentifier.UserId,
                    FileName = imageProcessedEvent.FileName,
                    Source = imageProcessedEvent.FileSource,
                    ThumbnailSmallFilePath = thumbSmallPath,
                    ThumbnailLargeFilePath = thumbLargePath,
                    // ThumbnailSmallFileId = thumbSmall,
                    // ThumbnailLargeFileId = thumbLarge,
                    Path = imageProcessedEvent.Path,
                    AddedOn = DateTimeOffset.UtcNow,
                    DateTimeTaken =
                        imageProcessedEvent.PhotoTakenOn ?? (imageProcessedEvent.FileCreatedOn ?? DateTimeOffset.UtcNow),
                    ExifString = JsonConvert.SerializeObject(imageProcessedEvent.ExifString),
                    Latitude = imageProcessedEvent.Latitude,
                    Longitude = imageProcessedEvent.Longitude,
                    HasGps = imageProcessedEvent.Latitude.HasValue && imageProcessedEvent.Longitude.HasValue
                };

                await photoService.AddAsync(photoEntity);
            }
        }
    }
}
