namespace PhotoMap.Worker.Services.Definitions
{
    public interface IImageConverter
    {
        /// <summary>
        /// Whether browsers need the image converted to be shown: not all of them can display HEIC/HEIF.
        /// </summary>
        bool NeedsConversion(string fileName);

        /// <summary>
        /// Converts the image to JPEG.
        /// </summary>
        byte[] ConvertToJpeg(byte[] fileContents);
    }
}
