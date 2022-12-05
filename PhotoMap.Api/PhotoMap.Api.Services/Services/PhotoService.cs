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

        public async Task<Photo> GetAsync(int id)
        {
            return await _photoRepository.GetAsync(id);
        }

        public async Task<Photo?> GetByFileNameAsync(string fileName)
        {
            return await _photoRepository.GetByFileNameAsync(fileName);
        }

        public async Task<ComplexResponse<Photo>> GetByUserIdAsync(int userId, int top, int skip)
        {
            return await _photoRepository.GetByUserIdAsync(userId, top, skip);
        }

        public async Task DeleteByUserId(int userId)
        {
            await _photoRepository.DeleteByUserIdAsync(userId);
        }

        public async Task DeleteAllAsync()
        {
            await _photoRepository.DeleteAllAsync();
        }
    }
}
