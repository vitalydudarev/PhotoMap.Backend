using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Domain.Services;
using FileInfo = PhotoMap.Api.Domain.Models.FileInfo;

namespace PhotoMap.Api.Services.Services;

public class FileService : IFileService
{
    private readonly IFileRepository _repository;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<FileService> _logger;

    public FileService(IFileRepository repository, IFileStorage fileStorage, ILogger<FileService> logger)
    {
        _repository = repository;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<Domain.Models.File> SaveAsync(string fileName, byte[] fileContents)
    {
        try
        {
            var filePath = await _fileStorage.SaveAsync(fileName, fileContents);

            var incomingFile = new Domain.Models.File
            {
                AddedOn = DateTime.UtcNow,
                FileName = fileName,
                FullPath = filePath,
                Size = fileContents.Length
            };
            
            var outgoingFileEntity = await _repository.AddAsync(incomingFile);

            return outgoingFileEntity;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to save {FileName}: {ExceptionMessage}", fileName, e.Message);
            throw;
        }
    }

    public async Task<byte[]> GetFileContentsAsync(long fileId)
    {
        try
        {
            var fileEntity = await _repository.GetAsync(fileId);
            if (fileEntity != null)
            {
                var fileContents = await _fileStorage.GetAsync(fileEntity.FileName);

                return fileContents;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to get {FileId}: {ExceptionMessage}", fileId, e.Message);
            throw;
        }

        return null;
    }

    public async Task<Domain.Models.FileInfo> GetFileInfoAsync(long fileId)
    {
        try
        {
            var fileEntity = await _repository.GetAsync(fileId);
            if (fileEntity != null)
            {
                return new FileInfo
                {
                    Id = fileEntity.Id,
                    FileName = fileEntity.FileName,
                    Size = fileEntity.Size,
                    AddedOn = fileEntity.AddedOn
                };
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to load file info for fileId {FileId}: {ExceptionMessage}", fileId, e.Message);
            throw;
        }

        return null;
    }

    public async Task<Domain.Models.FileInfo> GetFileInfoByFileNameAsync(string fileName)
    {
        try
        {
            var fileEntity = await _repository.GetByFileNameAsync(fileName);
            if (fileEntity != null)
            {
                return new FileInfo
                {
                    Id = fileEntity.Id,
                    FileName = fileEntity.FileName,
                    Size = fileEntity.Size,
                    AddedOn = fileEntity.AddedOn
                };
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to load file info for fileName {FileName}: {ExceptionMessage}", fileName, e.Message);
            throw;
        }

        return null;
    }

    public async Task DeleteFileAsync(long fileId)
    {
        try
        {
            var fileEntity = await _repository.GetAsync(fileId);
            if (fileEntity != null)
            {
                _fileStorage.Delete(fileEntity.FileName);
                await _repository.DeleteAsync(fileId);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to delete file for fileId {FileId}: {ExceptionMessage}", fileId, e.Message);
            throw;
        }
    }

    public async Task DeleteAllFilesAsync()
    {
        try
        {
            var files = await _repository.GetAllAsync();

            foreach (var file in files)
            {
                _fileStorage.Delete(file.FileName);
            }

            await _repository.DeleteAllAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to delete all files: {ExceptionMessage}", e.Message);
            throw;
        }
    }
}
