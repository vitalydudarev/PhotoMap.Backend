using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services
{
    public interface IPhotoService
    {
        Task AddAsync(Photo photo);

        Task<Photo> GetAsync(int id);

        Task<Photo?> GetByFileNameAsync(string fileName);

        Task<IEnumerable<Photo>> GetByUserIdAsync(int userId, int top, int skip);
        
        Task<int> GetTotalCountByUserIdAsync(int userId);

        Task DeleteByUserId(int userId);

        Task DeleteAllAsync();
    }
}
