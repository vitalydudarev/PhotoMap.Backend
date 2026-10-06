using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain
{
    public class PhotoService : IPhotoService
    {
        private readonly IPhotoRepository _photoRepository;
        private readonly PhotoYearsCache _yearsCache;
        private readonly PhotoCategorizationSignal _categorizationSignal;
        private readonly PhotoDuplicatesSignal _duplicatesSignal;

        public PhotoService(IPhotoRepository photoRepository, PhotoYearsCache yearsCache,
            PhotoCategorizationSignal categorizationSignal, PhotoDuplicatesSignal duplicatesSignal)
        {
            _photoRepository = photoRepository;
            _yearsCache = yearsCache;
            _categorizationSignal = categorizationSignal;
            _duplicatesSignal = duplicatesSignal;
        }

        public async Task AddRangeAsync(IReadOnlyCollection<Photo> photos)
        {
            await _photoRepository.AddRangeAsync(photos);

            foreach (var userPhotos in photos.GroupBy(a => a.UserId))
            {
                _yearsCache.AddYears(userPhotos.Key, userPhotos.Select(a => a.DateTimeTaken.UtcDateTime.Year));
            }

            // the photos are saved in none of the categories, until the categorization gets to them
            _categorizationSignal.Notify();
            // and in no group of copies, until the duplicates are looked for again
            _duplicatesSignal.Notify();
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

        public Task<IReadOnlyList<Photo>> GetGeotaggedAsync(long userId)
        {
            return _photoRepository.GetGeotaggedAsync(userId);
        }

        public Task<IReadOnlyList<int>> GetYearsAsync(long userId)
        {
            return _yearsCache.GetOrLoadAsync(userId, () => _photoRepository.GetYearsAsync(userId));
        }

        public Task<IReadOnlyList<Photo>> GetDuplicatesAsync(long userId)
        {
            return _photoRepository.GetDuplicatesAsync(userId);
        }

        public Task<bool> MarkAsDeletedAsync(long userId, long photoId)
        {
            return SetDeletedOnAsync(userId, photoId, DateTimeOffset.UtcNow);
        }

        public Task<bool> RestoreAsync(long userId, long photoId)
        {
            return SetDeletedOnAsync(userId, photoId, null);
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
                // a photo left alone in its group of copies is no longer a duplicate
                _duplicatesSignal.Notify();
            }
        }

        /// <summary>
        /// A photo marked as deleted leaves its group of copies, and one restored goes back to it.
        /// </summary>
        private async Task<bool> SetDeletedOnAsync(long userId, long photoId, DateTimeOffset? deletedOn)
        {
            var updated = await _photoRepository.SetDeletedOnAsync(userId, photoId, deletedOn);
            if (updated)
            {
                _duplicatesSignal.Notify();
            }

            return updated;
        }
    }
}
