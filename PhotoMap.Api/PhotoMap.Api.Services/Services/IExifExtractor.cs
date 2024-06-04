using PhotoMap.Api.Services.Models.Image;

namespace PhotoMap.Api.Services.Services
{
    public interface IExifExtractor
    {
        ExifData GetDataAsync(byte[] bytes);
    }
}
