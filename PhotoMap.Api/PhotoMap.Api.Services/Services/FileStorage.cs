using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using File = System.IO.File;

namespace PhotoMap.Api.Services.Services;

public class FileStorage : IFileStorage
{
    private readonly string _baseDirectory;

    public FileStorage(FileStorageSettings fileStorageSettings)
    {
        _baseDirectory = fileStorageSettings.BasePath;
    }

    public Task<byte[]> GetAsync(string fileName)
    {
        string filePath = GetFilePath(fileName);

        return File.ReadAllBytesAsync(filePath);
    }

    public async Task<string> SaveAsync(string fileName, byte[] bytes)
    {
        string filePath = GetFilePath(fileName);

        var dir = Path.GetDirectoryName(filePath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllBytesAsync(filePath, bytes);

        return filePath;
    }

    public void Delete(string fileName)
    {
        string filePath = GetFilePath(fileName);

        File.Delete(filePath);
    }

    private string GetFilePath(string fileName) => Path.Combine(_baseDirectory, fileName);
}
