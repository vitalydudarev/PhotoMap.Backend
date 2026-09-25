using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain
{
    public class PhotoService : IPhotoService
    {
        private readonly IPhotoRepository _photoRepository;
        private readonly PhotoYearsCache _yearsCache;

        public PhotoService(IPhotoRepository photoRepository, PhotoYearsCache yearsCache)
        {
            _photoRepository = photoRepository;
            _yearsCache = yearsCache;
        }

        public async Task AddRangeAsync(IReadOnlyCollection<Photo> photos)
        {
            await _photoRepository.AddRangeAsync(photos);

            foreach (var userPhotos in photos.GroupBy(a => a.UserId))
            {
                _yearsCache.AddYears(userPhotos.Key, userPhotos.Select(a => a.DateTimeTaken.UtcDateTime.Year));
            }
        }

        public Task<Photo?> GetAsync(long id)
        {
            return _photoRepository.GetAsync(id);
        }

        public Task<IReadOnlySet<string>> GetSavedExternalIdsAsync(long userId, long photoSourceId, IEnumerable<string> externalIds)
        {
            return _photoRepository.GetSavedExternalIdsAsync(userId, photoSourceId, externalIds);
        }

        public Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, PhotoFilter filter, int top, int skip,
            PhotoSortOrder sortOrder)
        {
            return _photoRepository.GetByUserIdAsync(userId, filter, top, skip, sortOrder);
        }

        public Task<int> GetTotalCountByUserIdAsync(long userId, PhotoFilter filter)
        {
            return _photoRepository.GetTotalCountByUserIdAsync(userId, filter);
        }

        public Task<IReadOnlyList<int>> GetYearsAsync(long userId)
        {
            return _yearsCache.GetOrLoadAsync(userId, () => _photoRepository.GetYearsAsync(userId));
        }

        public async Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId)
        {
            try
            {
                return await _photoRepository.DeleteByPhotoSourceAsync(userId, photoSourceId);
            }
            finally
            {
                // a delete that failed may still have taken some of the photos
                _yearsCache.Invalidate(userId);
            }
        }
    }
}
