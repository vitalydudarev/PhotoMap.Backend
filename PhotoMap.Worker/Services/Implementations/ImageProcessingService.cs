using System.Security.Cryptography;
using System.Text.Json;
using PhotoMap.Shared.Models;
using PhotoMap.Worker.Helpers;
using PhotoMap.Worker.Services.Definitions;
using PhotoMap.Worker.Services.Implementations.Core;

namespace PhotoMap.Worker.Services.Implementations
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly ILogger<ImageProcessingService> _logger;
        private readonly IExifExtractor _exifExtractor;

        public ImageProcessingService(ILogger<ImageProcessingService> logger, IExifExtractor exifExtractor)
        {
            _logger = logger;
            _exifExtractor = exifExtractor;
        }

        public ProcessedImage ProcessImage(ProcessImageRequest request)
        {
            var fileInfo = request.DownloadedFileInfo;

            _logger.LogInformation("Processing image {FileName}", fileInfo.ResourceName);

            var fileContents = File.ReadAllBytes(request.FileName);

            // thrown rather than processed: the file is then recorded as failed, to be downloaded again later
            if (fileContents.Length == 0)
            {
                throw new InvalidDataException("The downloaded file is empty.");
            }

            var sizes = request.Sizes.ToArray();

            using var imageProcessor = new ImageProcessor(fileContents, sizes.Max());
            imageProcessor.Rotate();
            
            var thumbnailFiles = new Dictionary<int, string>();

            foreach (var size in sizes)
            {
                imageProcessor.Crop(size);

                thumbnailFiles.Add(size, StageThumbnail(request, size, imageProcessor.GetImageBytes()));
            }
            
            var processedImage = new ProcessedImage
            {
                FileName = fileInfo.ResourceName,
                ThumbnailFiles = thumbnailFiles,
                Path = fileInfo.Path,
                ExternalId = fileInfo.FileId,
                ContentHash = Convert.ToHexStringLower(SHA256.HashData(fileContents)),
                FileCreatedOn = fileInfo.CreatedOn,
                UserId = request.UserId,
                PhotoSourceId = request.PhotoSourceId,
                PhotoSourceName = request.PhotoSourceName,
                FromOtherUploads = fileInfo.FromOtherUploads
            };

            var exif = _exifExtractor.GetDataAsync(fileContents);
            if (exif != null)
            {
                processedImage.PhotoTakenOn = ExifHelper.GetDate(exif);
                processedImage.Latitude = ExifHelper.GetLatitude(exif);
                processedImage.Longitude = ExifHelper.GetLongitude(exif);
                processedImage.HorizontalPositioningError = exif.Gps?.HorizontalPositioningError;
                processedImage.ExifString = JsonSerializer.Serialize(exif);
            }

            _logger.LogInformation("Processed image {FileName}", fileInfo.ResourceName);

            return processedImage;
        }

        /// <summary>
        /// Next to the downloaded file, which is unique to the download, until the photo has been saved: the API
        /// reads the thumbnail from there, then deletes it. Both need to see the same disk, as they do in one process.
        /// </summary>
        /// <returns>The path of the thumbnail.</returns>
        public static string StageThumbnail(ProcessImageRequest request, int size, byte[] bytes)
        {
            var path = $"{request.FileName}.{size}.jpg";

            File.WriteAllBytes(path, bytes);

            return path;
        }
    }
}
