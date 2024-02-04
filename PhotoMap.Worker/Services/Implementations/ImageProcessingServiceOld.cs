using System.Text.Json;
using Microsoft.Extensions.Options;
using PhotoMap.Shared.Models;
using PhotoMap.Worker.Helpers;
using PhotoMap.Worker.Models;
using PhotoMap.Worker.Services.Definitions;
using PhotoMap.Worker.Services.Implementations.Core;
using PhotoMap.Worker.Settings;

namespace PhotoMap.Worker.Services.Implementations
{
    public class ImageProcessingServiceOld : IImageProcessingServiceOld
    {
        private readonly ILogger<ImageProcessingServiceOld> _logger;
        private readonly ImageProcessingSettings _imageProcessingSettings;
        private readonly IExifExtractor _exifExtractor;

        public ImageProcessingServiceOld(
            ILogger<ImageProcessingServiceOld> logger,
            IOptions<ImageProcessingSettings> imageProcessingOptions,
            IExifExtractor exifExtractor)
        {
            _logger = logger;
            _imageProcessingSettings = imageProcessingOptions.Value;
            _exifExtractor = exifExtractor;
        }

        public async Task<ProcessedDownloadedFile> ProcessImageAsync(DownloadedFileInfo downloadedFile)
        {
            byte[] fileContents = [];
            using var imageProcessor = new ImageProcessor(fileContents);
            imageProcessor.Rotate();

            // var sizeFileIdMap = new Dictionary<int, long>();

            var sizeBytesMap = new Dictionary<int, byte[]>();

            foreach (var size in _imageProcessingSettings.Sizes)
            {
                imageProcessor.Crop(size);
                var bytes = imageProcessor.GetImageBytes();
                
                sizeBytesMap.Add(size, bytes);

                // var savedFile = await _imageUploadService.SaveThumbnailAsync(bytes, downloadedFile.ResourceName,
                    // downloadedFile.UserName, downloadedFile.Source, size);

                // sizeFileIdMap.Add(size, savedFile.Id);
            }
            
            var processedFile = new ProcessedDownloadedFile
            {
                FileName = downloadedFile.ResourceName,
                // FileSource = downloadedFile.Source,
                Thumbs = sizeBytesMap,
                Path = downloadedFile.Path,
                FileCreatedOn = downloadedFile.CreatedOn
            };

            var exif = _exifExtractor.GetDataAsync(fileContents);
            if (exif != null)
            {
                processedFile.PhotoTakenOn = ExifHelper.GetDate(exif);
                processedFile.Latitude = ExifHelper.GetLatitude(exif);
                processedFile.Longitude = ExifHelper.GetLongitude(exif);
                processedFile.ExifString = JsonSerializer.Serialize(exif);
            }

            return processedFile;
        }
    }
}
