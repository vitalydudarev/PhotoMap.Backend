namespace PhotoMap.Api.Domain.Services;

public interface IFileStorage
{
    Task<string> SaveAsync(string fileName, byte[] bytes);
    Task<byte[]> GetAsync(string fileName);
    void Delete(string fileName);

    /// <summary>
    /// Saves the file from the contents written into the stream given, which go to a file of their own until all of
    /// them have been written: a file that fails partway is not left behind as if it were whole.
    /// </summary>
    Task SaveAsync(string fileName, Func<Stream, CancellationToken, Task> writeContentsAsync, CancellationToken cancellationToken);

    /// <returns>The size of the file in bytes, null when there is no such file.</returns>
    long? GetSize(string fileName);

    /// <returns>The path of the file or folder on the disk.</returns>
    string GetFullPath(string fileName);
}
