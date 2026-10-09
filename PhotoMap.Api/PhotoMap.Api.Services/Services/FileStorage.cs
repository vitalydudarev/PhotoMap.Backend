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

    public async Task SaveAsync(string fileName, Func<Stream, CancellationToken, Task> writeContentsAsync,
        CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(fileName);

        var dir = Path.GetDirectoryName(filePath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var partialFilePath = filePath + ".part";

        try
        {
            await using (var stream = new FileStream(partialFilePath, FileMode.Create, FileAccess.Write, FileShare.None,
                             bufferSize: 81920, useAsync: true))
            {
                await writeContentsAsync(stream, cancellationToken);
            }

            File.Move(partialFilePath, filePath, overwrite: true);
        }
        catch
        {
            File.Delete(partialFilePath);
            throw;
        }
    }

    public long? GetSize(string fileName)
    {
        var fileInfo = new FileInfo(GetFilePath(fileName));

        return fileInfo.Exists ? fileInfo.Length : null;
    }

    public string GetFullPath(string fileName) => GetFilePath(fileName);

    private string GetFilePath(string fileName) => Path.Combine(_baseDirectory, fileName);
}
