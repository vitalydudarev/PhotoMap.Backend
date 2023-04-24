using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services
{
    public class PhotoService : IPhotoService
    {
        private readonly IPhotoRepository _photoRepository;

        public PhotoService(IPhotoRepository photoRepository)
        {
            _photoRepository = photoRepository;
        }

        public async Task AddAsync(Photo photo)
        {
            await _photoRepository.AddAsync(photo);
        }

        public Task<Photo?> GetAsync(long id)
        {
            return _photoRepository.GetAsync(id);
        }

        public Task<Photo?> GetByFileNameAsync(string fileName)
        {
            return _photoRepository.GetByFileNameAsync(fileName);
        }

        public Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, int top, int skip)
        {
            return _photoRepository.GetByUserIdAsync(userId, top, skip);
        }

        public Task<int> GetTotalCountByUserIdAsync(long userId)
        {
            return _photoRepository.GetTotalCountByUserIdAsync(userId);
        }

        public async Task DeleteByUserId(long userId)
        {
            await _photoRepository.DeleteByUserIdAsync(userId);
        }

        public async Task DeleteAllAsync()
        {
            await _photoRepository.DeleteAllAsync();
        }
    }
}
