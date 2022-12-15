namespace PhotoMap.Api.Domain.Services;

public interface IFileService
{
    Task<Domain.Models.File> SaveAsync(string fileName, byte[] fileContents);
    Task<byte[]> GetFileContentsAsync(long fileId);
    Task<Models.FileInfo> GetFileInfoAsync(long fileId);
    Task<Models.FileInfo> GetFileInfoByFileNameAsync(string fileName);
    Task DeleteFileAsync(long fileId);
    Task DeleteAllFilesAsync();
}
