using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Queues;

namespace PhotoMap.Api.Services
{
    /// <summary>
    /// Consumes the images processed by PhotoMap.Worker from the in-process queue,
    /// stores their thumbnails and saves the photos, several at a time.
    /// </summary>
    public class ProcessedImageBackgroundService : BackgroundService
    {
        private readonly IMessageQueue<ProcessedImage> _processedImageQueue;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ProcessedImageBackgroundService> _logger;
        private readonly int _batchSize;
        private readonly TimeSpan _batchMaxWait;

        public ProcessedImageBackgroundService(
            IMessageQueue<ProcessedImage> processedImageQueue,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<ProcessedImageBackgroundService> logger,
            IOptions<PhotoProcessingSettings> settings)
        {
            _processedImageQueue = processedImageQueue;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _batchSize = Math.Max(settings.Value.SaveBatchSize, 1);
            _batchMaxWait = settings.Value.SaveBatchMaxWait;
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
                while (true)
                {
                    var processedImages = await _processedImageQueue.ReadBatchAsync(_batchSize, _batchMaxWait, stoppingToken);
                    if (processedImages.Count == 0)
                    {
                        break;
                    }

                    await SaveBatchAsync(processedImages);
                }
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
        }

        /// <summary>
        /// Saves the photos with one query for the ones saved already and one insert for the rest. When the insert
        /// fails, e.g. on one photo saved in the meantime, the photos are saved one at a time, so that only the
        /// photos that cannot be saved fail. A photo is reported processed only once it is in the database.
        /// </summary>
        private async Task SaveBatchAsync(IReadOnlyList<ProcessedImage> processedImages)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var photoService = scope.ServiceProvider.GetRequiredService<IPhotoService>();
                var imageStore = scope.ServiceProvider.GetRequiredService<IImageStore>();

                var newImages = await GetNewImagesAsync(photoService, processedImages);

                var photos = new List<(ProcessedImage Image, Photo Photo)>();

                foreach (var processedImage in newImages)
                {
                    try
                    {
                        photos.Add((processedImage, await CreatePhotoAsync(imageStore, processedImage)));
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to store the thumbnails of {FileName}.", processedImage.FileName);

                        processedImage.Processed?.TrySetResult(
                            ProcessingResult.Failed("Failed to store the thumbnails: " + e.Message));
                    }
                }

                if (photos.Count > 0)
                {
                    await photoService.AddRangeAsync(photos.Select(a => a.Photo).ToList());
                }

                foreach (var (processedImage, _) in photos)
                {
                    processedImage.Processed?.TrySetResult(ProcessingResult.Success);
                }

                _logger.LogInformation("Saved {SavedCount} of {ImageCount} processed images", photos.Count,
                    processedImages.Count);
            }
            catch (Exception e) when (processedImages.Count > 1)
            {
                _logger.LogWarning(e, "Failed to save {ImageCount} processed images at once, saving them one at a time",
                    processedImages.Count);

                foreach (var processedImage in processedImages)
                {
                    await SaveBatchAsync([processedImage]);
                }
            }
            catch (Exception e)
            {
                var processedImage = processedImages[0];

                _logger.LogError(e, "Failed to save processed image {FileName}.", processedImage.FileName);

                processedImage.Processed?.TrySetResult(ProcessingResult.Failed("Failed to save the photo: " + e.Message));
            }
        }

        /// <summary>
        /// The images whose photos are not saved yet, the others are reported processed right away. The same file
        /// twice in the batch fails the insert on the unique index, the second is then found saved one at a time.
        /// </summary>
        private async Task<List<ProcessedImage>> GetNewImagesAsync(IPhotoService photoService,
            IReadOnlyList<ProcessedImage> processedImages)
        {
            var newImages = new List<ProcessedImage>();

            // the images of a batch almost always come from one run, so this is usually one query
            foreach (var sourceImages in processedImages.GroupBy(a => (a.UserId, a.PhotoSourceId)))
            {
                var externalIds = sourceImages.Select(a => a.ExternalId).OfType<string>().ToList();
                var savedExternalIds = await photoService.GetSavedExternalIdsAsync(sourceImages.Key.UserId,
                    sourceImages.Key.PhotoSourceId, externalIds);

                foreach (var processedImage in sourceImages)
                {
                    if (processedImage.ExternalId != null && savedExternalIds.Contains(processedImage.ExternalId))
                    {
                        _logger.LogInformation("Image {FileName} already saved, skipping", processedImage.FileName);

                        processedImage.Processed?.TrySetResult(ProcessingResult.Success);
                        continue;
                    }

                    newImages.Add(processedImage);
                }
            }

            return newImages;
        }

        private static async Task<Photo> CreatePhotoAsync(IImageStore imageStore, ProcessedImage processedImage)
        {
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

            return new Photo
            {
                UserId = processedImage.UserId,
                PhotoSourceId = processedImage.PhotoSourceId,
                FileName = processedImage.FileName,
                ThumbnailSmallFilePath = thumbnailSmallFilePath,
                ThumbnailLargeFilePath = thumbnailLargeFilePath,
                Path = processedImage.Path,
                ExternalId = processedImage.ExternalId,
                ContentHash = processedImage.ContentHash,
                AddedOn = DateTimeOffset.UtcNow,
                DateTimeTaken = ToUtc(processedImage.PhotoTakenOn) ?? ToUtc(processedImage.FileCreatedOn) ?? DateTimeOffset.UtcNow,
                ExifString = processedImage.ExifString,
                Latitude = processedImage.Latitude,
                Longitude = processedImage.Longitude,
                HasGps = processedImage.Latitude.HasValue && processedImage.Longitude.HasValue
            };
        }

        /// <summary>
        /// Dates are stored in timestamptz columns, which only accept UTC. Photo sources report
        /// UTC dates, but not all of them mark the value as such.
        /// </summary>
        private static DateTimeOffset? ToUtc(DateTime? value)
        {
            if (value == null)
            {
                return null;
            }

            var dateTime = value.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : value.Value.ToUniversalTime();

            return new DateTimeOffset(dateTime);
        }
    }
}
