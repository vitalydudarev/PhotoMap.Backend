using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PhotoMap.Worker.Helpers;
using PhotoMap.Worker.Models;
using PhotoMap.Worker.Services.Definitions;
using PhotoMap.Worker.Services.Implementations.Core;
using PhotoMap.Worker.Settings;

namespace PhotoMap.Worker.Services.Implementations
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly ILogger<ImageProcessingService> _logger;
        private readonly ImageProcessingSettings _imageProcessingSettings;
        private readonly IExifExtractor _exifExtractor;

        public ImageProcessingService(
            ILogger<ImageProcessingService> logger,
            IOptions<ImageProcessingSettings> imageProcessingOptions,
            IExifExtractor exifExtractor)
        {
            _logger = logger;
            _imageProcessingSettings = imageProcessingOptions.Value;
            _exifExtractor = exifExtractor;
        }

        public async Task<ProcessedDownloadedFile> ProcessImageAsync(DownloadedFileInfo downloadedFile)
        {
            using var imageProcessor = new ImageProcessor(downloadedFile.FileContents);
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
                FileSource = downloadedFile.Source,
                Thumbs = sizeBytesMap,
                Path = downloadedFile.Path,
                FileCreatedOn = downloadedFile.CreatedOn
            };

            var exif = _exifExtractor.GetDataAsync(downloadedFile.FileContents);
            if (exif != null)
            {
                processedFile.PhotoTakenOn = ExifHelper.GetDate(exif);
                processedFile.Latitude = ExifHelper.GetLatitude(exif);
                processedFile.Longitude = ExifHelper.GetLongitude(exif);
                // TODO: switch to System.Text.Json serializer
                processedFile.ExifString = JsonConvert.SerializeObject(exif);
            }

            return processedFile;
        }
    }
}
