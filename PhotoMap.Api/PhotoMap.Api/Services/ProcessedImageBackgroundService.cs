using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Events;
using PhotoMap.Shared.Messages;
using PhotoMap.Shared.Messaging;
using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services
{
    /// <summary>
    /// Receives what PhotoMap.Worker made of the images, <see cref="PhotoProcessed"/>, through Kafka or in memory,
    /// stores the thumbnails and saves the photos, several at a time, and reports each result to the run waiting
    /// for it. Saves a <see cref="PhotoSavedEvent"/> to the outbox with each photo, for the outbox relay to publish.
    /// </summary>
    /// <remarks>
    /// The messages are acknowledged once their batch has been handled: after a restart, Kafka hands again the ones
    /// handled partly or not at all (at-least-once). Handling one twice does no harm, the photos saved are skipped.
    /// </remarks>
    public class ProcessedImageBackgroundService : BackgroundService
    {
        public const string GroupId = "photo-map.photo-processed-saver";

        private readonly IMessageReceiver<PhotoProcessed> _receiver;
        private readonly ProcessingRequests _processingRequests;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ProcessedImageBackgroundService> _logger;
        private readonly KafkaSettings _kafkaSettings;
        private readonly OutboxSignal _outboxSignal;
        private readonly int _batchSize;
        private readonly TimeSpan _batchMaxWait;

        public ProcessedImageBackgroundService(
            IMessageReceiver<PhotoProcessed> receiver,
            ProcessingRequests processingRequests,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<ProcessedImageBackgroundService> logger,
            IOptions<PhotoProcessingSettings> settings,
            KafkaSettings kafkaSettings,
            OutboxSignal outboxSignal)
        {
            _receiver = receiver;
            _processingRequests = processingRequests;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _kafkaSettings = kafkaSettings;
            _outboxSignal = outboxSignal;
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
                    var messages = await _receiver.ReceiveBatchAsync(_batchSize, _batchMaxWait, stoppingToken);
                    if (messages.Count == 0)
                    {
                        break;
                    }

                    await HandleBatchAsync(messages);

                    _receiver.Acknowledge();
                }
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
        }

        private async Task HandleBatchAsync(IReadOnlyList<PhotoProcessed> messages)
        {
            foreach (var failed in messages.Where(a => !a.Succeeded))
            {
                Complete(failed.RequestId, ProcessingResult.Failed(failed.Error ?? "Failed to process the image."));
            }

            var images = messages
                .Where(a => a.Succeeded)
                .Select(a => new ReceivedImage(a.RequestId, a.Image!))
                .ToList();

            if (images.Count == 0)
            {
                return;
            }

            await SaveBatchAsync(images);

            // Before the messages are acknowledged: a message handed again after a restart finds its photo saved,
            // and needs its thumbnails no longer; were they deleted after, a restart in between would leave them
            DeleteThumbnailFiles(images);
        }

        /// <summary>
        /// Saves the photos with one query for the ones saved already and one insert for the rest. When the insert
        /// fails, e.g. on one photo saved in the meantime, the photos are saved one at a time, so that only the
        /// photos that cannot be saved fail. A photo is reported processed only once it is in the database.
        /// </summary>
        private async Task SaveBatchAsync(IReadOnlyList<ReceivedImage> images)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var photoService = scope.ServiceProvider.GetRequiredService<IPhotoService>();
                var imageStore = scope.ServiceProvider.GetRequiredService<IImageStore>();

                var newImages = await GetNewImagesAsync(photoService, images);

                var photos = new List<(ReceivedImage Received, Photo Photo)>();

                foreach (var received in newImages)
                {
                    try
                    {
                        photos.Add((received, await CreatePhotoAsync(imageStore, received.Image)));
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to store the thumbnails of {FileName}.", received.Image.FileName);

                        Complete(received.RequestId, ProcessingResult.Failed("Failed to store the thumbnails: " + e.Message));
                    }
                }

                if (photos.Count > 0)
                {
                    // The events are saved with the photos, in one transaction, rather than published to Kafka
                    // after: a photo is saved exactly when its event is, neither is lost when the other is not
                    // (the outbox pattern). Kafka disabled, there is nothing to publish them to.
                    var outboxMessages = _kafkaSettings.Enabled
                        ? photos.Select(a => CreateOutboxMessage(a.Received.Image, a.Photo)).ToList()
                        : [];

                    await photoService.AddRangeAsync(photos.Select(a => a.Photo).ToList(), outboxMessages);

                    if (outboxMessages.Count > 0)
                    {
                        _outboxSignal.Notify();
                    }
                }

                foreach (var (received, _) in photos)
                {
                    Complete(received.RequestId, ProcessingResult.Success);
                }

                _logger.LogInformation("Saved {SavedCount} of {ImageCount} processed images", photos.Count, images.Count);
            }
            catch (Exception e) when (images.Count > 1)
            {
                _logger.LogWarning(e, "Failed to save {ImageCount} processed images at once, saving them one at a time",
                    images.Count);

                foreach (var received in images)
                {
                    await SaveBatchAsync([received]);
                }
            }
            catch (Exception e)
            {
                var received = images[0];

                _logger.LogError(e, "Failed to save processed image {FileName}.", received.Image.FileName);

                Complete(received.RequestId, ProcessingResult.Failed("Failed to save the photo: " + e.Message));
            }
        }

        /// <summary>
        /// The images whose photos are not saved yet, the others are reported processed right away. The same file
        /// twice in the batch fails the insert on the unique index, the second is then found saved one at a time.
        /// </summary>
        private async Task<List<ReceivedImage>> GetNewImagesAsync(IPhotoService photoService,
            IReadOnlyList<ReceivedImage> images)
        {
            var newImages = new List<ReceivedImage>();

            // the images of a batch almost always come from one run, so this is usually one query
            foreach (var sourceImages in images.GroupBy(a => (a.Image.UserId, a.Image.PhotoSourceId)))
            {
                var externalIds = sourceImages.Select(a => a.Image.ExternalId).OfType<string>().ToList();
                var savedExternalIds = await photoService.GetSavedExternalIdsAsync(sourceImages.Key.UserId,
                    sourceImages.Key.PhotoSourceId, externalIds);

                foreach (var received in sourceImages)
                {
                    if (received.Image.ExternalId != null && savedExternalIds.Contains(received.Image.ExternalId))
                    {
                        _logger.LogInformation("Image {FileName} already saved, skipping", received.Image.FileName);

                        Complete(received.RequestId, ProcessingResult.Success);
                        continue;
                    }

                    newImages.Add(received);
                }
            }

            return newImages;
        }

        private void Complete(Guid requestId, ProcessingResult result)
        {
            if (!_processingRequests.Complete(requestId, result))
            {
                // e.g. requested before a restart: the photo is saved all the same, nothing waits to count it
                _logger.LogDebug("Nothing waits for the result of request {RequestId}", requestId);
            }
        }

        private void DeleteThumbnailFiles(IEnumerable<ReceivedImage> images)
        {
            foreach (var path in images.SelectMany(a => a.Image.ThumbnailFiles.Values))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Failed to delete the thumbnail {FilePath}", path);
                }
            }
        }

        private static async Task<Photo> CreatePhotoAsync(IImageStore imageStore, ProcessedImage processedImage)
        {
            var thumbs = processedImage.ThumbnailFiles.OrderBy(a => a.Key).ToList();
            var userName = processedImage.UserId.ToString();

            string? thumbnailSmallFilePath = null;
            string? thumbnailLargeFilePath = null;

            if (thumbs.Count > 0)
            {
                var thumbSmall = thumbs.First();
                var thumbLarge = thumbs.Last();

                // the worker has left the thumbnails in files, the message only says where (claim-check)
                thumbnailSmallFilePath = await imageStore.SaveThumbnailAsync(await File.ReadAllBytesAsync(thumbSmall.Value),
                    processedImage.FileName, processedImage.ExternalId, userName, processedImage.PhotoSourceName,
                    thumbSmall.Key);

                thumbnailLargeFilePath = await imageStore.SaveThumbnailAsync(await File.ReadAllBytesAsync(thumbLarge.Value),
                    processedImage.FileName, processedImage.ExternalId, userName, processedImage.PhotoSourceName,
                    thumbLarge.Key);
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
                HorizontalPositioningError = processedImage.HorizontalPositioningError,
                HasGps = processedImage.Latitude.HasValue && processedImage.Longitude.HasValue,
                FromOtherUploads = processedImage.FromOtherUploads
            };
        }

        private static OutboxMessage CreateOutboxMessage(ProcessedImage processedImage, Photo photo)
        {
            var photoSaved = new PhotoSavedEvent(photo.UserId, photo.PhotoSourceId, processedImage.PhotoSourceName,
                photo.FileName, photo.Path, photo.ExternalId, photo.ContentHash, photo.DateTimeTaken, photo.Latitude,
                photo.Longitude, photo.FromOtherUploads, photo.AddedOn);

            return new OutboxMessage
            {
                Topic = PhotoSavedEvent.Topic,
                Key = photoSaved.GetKey(),
                Payload = JsonSerializer.Serialize(photoSaved),
                CreatedOn = photo.AddedOn
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

        /// <param name="RequestId">Matches the image back to the run waiting for it.</param>
        private sealed record ReceivedImage(Guid RequestId, ProcessedImage Image);
    }
}
