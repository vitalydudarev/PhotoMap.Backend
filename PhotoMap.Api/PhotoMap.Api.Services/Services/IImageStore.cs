namespace PhotoMap.Api.Services.Services;

public interface IImageStore
{
    Task<string> SaveThumbnailAsync(byte[] bytes, string fileName, string userName, string source, int size);
}