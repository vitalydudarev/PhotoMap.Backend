using System.Text.Json;
using Microsoft.Extensions.Logging;
using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Services
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

        public ProcessedImage ProcessImage(DownloadedFileInfo fileInfo, byte[] fileContents, IEnumerable<int> thumbSizes)
        {
            _logger.LogInformation("Processing image {FileName}", fileInfo.ResourceName);

            using var imageProcessor = new ImageProcessor(fileContents);
            imageProcessor.Rotate();
            
            var sizeBytesMap = new Dictionary<int, byte[]>();

            foreach (var size in thumbSizes)
            {
                imageProcessor.Crop(size);
                var bytes = imageProcessor.GetImageBytes();
                
                sizeBytesMap.Add(size, bytes);
            }
            
            var exif = _exifExtractor.GetDataAsync(fileContents);
            
            var processedImage = new ProcessedImage
            {
                FileName = fileInfo.ResourceName,
                Thumbs = sizeBytesMap,
                Path = fileInfo.Path,
                FileCreatedOn = fileInfo.CreatedOn,
                PhotoTakenOn = ExifHelper.GetDate(exif),
                Latitude = ExifHelper.GetLatitude(exif),
                Longitude = ExifHelper.GetLongitude(exif),
                ExifString = JsonSerializer.Serialize(exif)
            };

            _logger.LogInformation("Processed image {FileName}", fileInfo.ResourceName);

            return processedImage;
        }
    }
}
