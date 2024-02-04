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

        public ProcessedImage ProcessImage(DownloadedFileInfo fileInfo, string fileName, IEnumerable<int> thumbSizes)
        {
            _logger.LogInformation("Processing image {FileName}", fileInfo.ResourceName);

            var fileContents = File.ReadAllBytes(fileName);

            using var imageProcessor = new ImageProcessor(fileContents);
            imageProcessor.Rotate();
            
            var sizeBytesMap = new Dictionary<int, byte[]>();

            foreach (var size in thumbSizes)
            {
                imageProcessor.Crop(size);
                var bytes = imageProcessor.GetImageBytes();
                
                sizeBytesMap.Add(size, bytes);
            }
            
            var processedImage = new ProcessedImage
            {
                FileName = fileInfo.ResourceName,
                Thumbs = sizeBytesMap,
                Path = fileInfo.Path,
                FileCreatedOn = fileInfo.CreatedOn
            };

            var exif = _exifExtractor.GetDataAsync(fileContents);
            if (exif != null)
            {
                processedImage.PhotoTakenOn = ExifHelper.GetDate(exif);
                processedImage.Latitude = ExifHelper.GetLatitude(exif);
                processedImage.Longitude = ExifHelper.GetLongitude(exif);
                processedImage.ExifString = JsonSerializer.Serialize(exif);
            }
            
            _logger.LogInformation("Processed image {FileName}", fileInfo.ResourceName);

            return processedImage;
        }
    }
}
