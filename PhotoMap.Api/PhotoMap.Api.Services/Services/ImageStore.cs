using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services;

public class ImageStore : IImageStore
{
    private readonly IFileStorage _fileStorage;

    public ImageStore(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }
    
    public async Task<string> GetThumbnailAsync(byte[] bytes, string fileName, string userName, string source, int size)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var thumbFileName = $"{fileNameWithoutExtension}_{size}{extension}";
        var path = Path.Combine(source, userName, "thumbs", thumbFileName);

        await _fileStorage.SaveAsync(path, bytes);

        return path;
    }

    public async Task<string> SaveImageAsync(byte[] bytes, string fileName, string userName, string source)
    {
        var filePath = Path.Combine(source, userName, fileName);

        await _fileStorage.SaveAsync(filePath, bytes);
        
        return filePath;
    }

    public async Task<string> SaveThumbnailAsync(byte[] bytes, string fileName, string userName, string source, int size)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var thumbFileName = $"{fileNameWithoutExtension}_{size}{extension}";
        var path = Path.Combine(source, userName, "thumbs", thumbFileName);

        await _fileStorage.SaveAsync(path, bytes);

        return path;
    }
}