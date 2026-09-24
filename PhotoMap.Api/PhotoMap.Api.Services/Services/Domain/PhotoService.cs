using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain
{
    public class PhotoService : IPhotoService
    {
        private readonly IPhotoRepository _photoRepository;

        public PhotoService(IPhotoRepository photoRepository)
        {
            _photoRepository = photoRepository;
        }

        public Task AddRangeAsync(IReadOnlyCollection<Photo> photos)
        {
            return _photoRepository.AddRangeAsync(photos);
        }

        public Task<Photo?> GetAsync(long id)
        {
            return _photoRepository.GetAsync(id);
        }

        public Task<IReadOnlySet<string>> GetSavedExternalIdsAsync(long userId, long photoSourceId, IEnumerable<string> externalIds)
        {
            return _photoRepository.GetSavedExternalIdsAsync(userId, photoSourceId, externalIds);
        }

        public Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, int top, int skip, PhotoSortOrder sortOrder)
        {
            return _photoRepository.GetByUserIdAsync(userId, top, skip, sortOrder);
        }

        public Task<int> GetTotalCountByUserIdAsync(long userId)
        {
            return _photoRepository.GetTotalCountByUserIdAsync(userId);
        }

        public Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId)
        {
            return _photoRepository.DeleteByPhotoSourceAsync(userId, photoSourceId);
        }

        public async Task DeleteAllAsync()
        {
            await _photoRepository.DeleteAllAsync();
        }
    }
}
