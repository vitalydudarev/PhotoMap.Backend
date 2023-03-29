using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Repositories;

public interface IPhotoRepository
{
    Task AddAsync(Photo photo);
    Task<Photo> GetAsync(int id);
    Task<Photo> GetByFileNameAsync(string fileName);
    Task<IEnumerable<Photo>> GetByUserIdAsync(int userId, int top, int skip);
    Task<int> GetTotalCountByUserIdAsync(int userId);
    Task DeleteByUserIdAsync(int userId);
    Task DeleteAllAsync();
}
