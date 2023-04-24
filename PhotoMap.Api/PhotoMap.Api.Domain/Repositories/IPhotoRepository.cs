using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Repositories;

public interface IPhotoRepository
{
    Task AddAsync(Photo photo);
    Task<Photo?> GetAsync(long id);
    Task<Photo?> GetByFileNameAsync(string fileName);
    Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, int top, int skip);
    Task<int> GetTotalCountByUserIdAsync(long userId);
    Task DeleteByUserIdAsync(long userId);
    Task DeleteAllAsync();
}
