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

        public ProcessedImage ProcessImage(DownloadedFileInfo downloadedFile, IEnumerable<int> thumbSizes)
        {
            _logger.LogInformation("Processing image {FileName}", downloadedFile.ResourceName);
            
            using var imageProcessor = new ImageProcessor(downloadedFile.FileContents);
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
                FileName = downloadedFile.ResourceName,
                Thumbs = sizeBytesMap,
                Path = downloadedFile.Path,
                FileCreatedOn = downloadedFile.CreatedOn
            };

            var exif = _exifExtractor.GetDataAsync(downloadedFile.FileContents);
            if (exif != null)
            {
                processedImage.PhotoTakenOn = ExifHelper.GetDate(exif);
                processedImage.Latitude = ExifHelper.GetLatitude(exif);
                processedImage.Longitude = ExifHelper.GetLongitude(exif);
                processedImage.ExifString = JsonSerializer.Serialize(exif);
            }
            
            _logger.LogInformation("Processed image {FileName}", downloadedFile.ResourceName);

            return processedImage;
        }
    }
}
