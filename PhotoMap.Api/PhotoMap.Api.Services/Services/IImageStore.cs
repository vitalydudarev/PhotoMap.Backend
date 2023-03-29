namespace PhotoMap.Api.Services.Services;

public interface IImageStore
{
    Task<string> SaveImageAsync(byte[] bytes, string fileName, string userName, string source);

    Task<string> SaveThumbnailAsync(byte[] bytes, string fileName, string userName, string source, int size);
}