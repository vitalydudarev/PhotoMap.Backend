using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PhotoMap.Worker.Helpers;
using PhotoMap.Worker.Models;
using PhotoMap.Worker.Models.Image;
using PhotoMap.Worker.Services.Definitions;
using PhotoMap.Worker.Services.Implementations.Core;
using PhotoMap.Worker.Settings;

namespace PhotoMap.Worker.Services.Implementations
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly ILogger<ImageProcessingService> _logger;
        private readonly ImageProcessingSettings _imageProcessingSettings;

        public ImageProcessingService(
            ILogger<ImageProcessingService> logger,
            IOptions<ImageProcessingSettings> imageProcessingOptions)
        {
            _logger = logger;
            _imageProcessingSettings = imageProcessingOptions.Value;
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

            // TODO: move this to a helper class
            DateTime? dateTimeTaken = null;
            string? exifString = null;
            double? longitude = null;
            double? latitude = null;

            var exifExtractor = new ExifExtractor();

            var exif = exifExtractor.GetDataAsync(downloadedFile.FileContents);
            if (exif != null)
            {
                dateTimeTaken = GetDate(exif);

                var gps = exif.Gps;
                if (gps != null)
                {
                    latitude = gps.Latitude != null && gps.LatitudeRef != null
                        ? GpsHelper.ConvertLatitude(gps.Latitude, gps.LatitudeRef)
                        : (double?) null;
                    longitude = gps.Longitude != null && gps.LongitudeRef != null
                        ? GpsHelper.ConvertLongitude(gps.Longitude, gps.LongitudeRef)
                        : (double?) null;
                }

                exifString = JsonConvert.SerializeObject(exif);
            }

            return new ProcessedDownloadedFile
            {
                FileName = downloadedFile.ResourceName,
                FileSource = downloadedFile.Source,
                Thumbs = sizeBytesMap,
                Path = downloadedFile.Path,
                FileCreatedOn = downloadedFile.CreatedOn,
                PhotoTakenOn = dateTimeTaken,
                ExifString = exifString,
                Latitude = latitude,
                Longitude = longitude
            };
        }

        private static DateTime? GetDate(ExifData exif)
        {
            return exif.Gps?.DateTimeStamp?.ToUniversalTime() ?? exif.ExifSubIfd?.DateTimeOriginal?.ToUniversalTime();
        }
    }
}
