using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services
{
    public interface IPhotoService
    {
        Task AddAsync(Photo photo);

        Task<Photo?> GetAsync(long id);

        Task<Photo?> GetByFileNameAsync(string fileName);

        Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, int top, int skip);
        
        Task<int> GetTotalCountByUserIdAsync(long userId);

        Task DeleteByUserId(long userId);

        Task DeleteAllAsync();
    }
}
