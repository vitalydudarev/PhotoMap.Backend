using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Queues;

namespace PhotoMap.Api.Services
{
    /// <summary>
    /// Consumes the images processed by PhotoMap.Worker from the in-process queue,
    /// stores their thumbnails and saves the photos.
    /// </summary>
    public class ProcessedImageBackgroundService : BackgroundService
    {
        private readonly IMessageQueue<ProcessedImage> _processedImageQueue;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ProcessedImageBackgroundService> _logger;

        public ProcessedImageBackgroundService(
            IMessageQueue<ProcessedImage> processedImageQueue,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<ProcessedImageBackgroundService> logger)
        {
            _processedImageQueue = processedImageQueue;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(ProcessedImageBackgroundService)} is stopping.");

            await base.StopAsync(stoppingToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(ProcessedImageBackgroundService)} is running.");

            try
            {
                await foreach (var processedImage in _processedImageQueue.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        await SavePhotoAsync(processedImage);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to save processed image {FileName}.", processedImage.FileName);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
        }

        private async Task SavePhotoAsync(ProcessedImage processedImage)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var photoService = scope.ServiceProvider.GetRequiredService<IPhotoService>();
            var imageStore = scope.ServiceProvider.GetRequiredService<IImageStore>();

            var thumbs = processedImage.Thumbs.OrderBy(a => a.Key).ToList();
            var userName = processedImage.UserId.ToString();

            string? thumbnailSmallFilePath = null;
            string? thumbnailLargeFilePath = null;

            if (thumbs.Count > 0)
            {
                var thumbSmall = thumbs.First();
                var thumbLarge = thumbs.Last();

                thumbnailSmallFilePath = await imageStore.SaveThumbnailAsync(thumbSmall.Value, processedImage.FileName,
                    userName, processedImage.PhotoSourceName, thumbSmall.Key);

                thumbnailLargeFilePath = await imageStore.SaveThumbnailAsync(thumbLarge.Value, processedImage.FileName,
                    userName, processedImage.PhotoSourceName, thumbLarge.Key);
            }

            var photo = new Photo
            {
                UserId = processedImage.UserId,
                PhotoSourceId = processedImage.PhotoSourceId,
                FileName = processedImage.FileName,
                ThumbnailSmallFilePath = thumbnailSmallFilePath,
                ThumbnailLargeFilePath = thumbnailLargeFilePath,
                Path = processedImage.Path,
                AddedOn = DateTimeOffset.UtcNow,
                DateTimeTaken = processedImage.PhotoTakenOn ?? processedImage.FileCreatedOn ?? DateTimeOffset.UtcNow,
                ExifString = processedImage.ExifString,
                Latitude = processedImage.Latitude,
                Longitude = processedImage.Longitude,
                HasGps = processedImage.Latitude.HasValue && processedImage.Longitude.HasValue
            };

            await photoService.AddAsync(photo);

            _logger.LogInformation("Image {FileName} processed and saved", processedImage.FileName);
        }
    }
}
