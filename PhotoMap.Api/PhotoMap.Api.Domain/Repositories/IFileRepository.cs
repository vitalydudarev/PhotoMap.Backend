namespace PhotoMap.Api.Domain.Repositories;

public interface IFileRepository
{
    Task<Models.File> AddAsync(Models.File incomingFile);
    Task<Models.File> GetAsync(long fileId);
    Task<Models.File> GetByFileNameAsync(string fileName);
    Task<IEnumerable<Models.File>> GetAllAsync();
    Task DeleteAsync(long fileId);
    Task DeleteAllAsync();
}
