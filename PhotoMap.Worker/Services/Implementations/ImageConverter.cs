using PhotoMap.Worker.Services.Definitions;
using PhotoMap.Worker.Services.Implementations.Core;

namespace PhotoMap.Worker.Services.Implementations
{
    public class ImageConverter : IImageConverter
    {
        private static readonly string[] ExtensionsToConvert = [".heic", ".heif"];

        public bool NeedsConversion(string fileName)
        {
            return ExtensionsToConvert.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
        }

        public byte[] ConvertToJpeg(byte[] fileContents)
        {
            using var imageProcessor = new ImageProcessor(fileContents);

            return imageProcessor.GetImageBytes();
        }
    }
}
