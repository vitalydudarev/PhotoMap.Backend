using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using Photo = PhotoMap.Api.Domain.Models.Photo;

namespace PhotoMap.Api.Database.Repositories;

public class PhotoRepository : IPhotoRepository
{
    private readonly PhotoMapContext _context;

    public PhotoRepository(PhotoMapContext context)
    {
        _context = context;
    }
    
    /// <summary>
    /// Saves the photos in one go: they are all saved, or none of them is.
    /// </summary>
    public async Task AddRangeAsync(IReadOnlyCollection<Photo> photos)
    {
        await _context.Photos.AddRangeAsync(photos.Select(ModelToEntity));
        await _context.SaveChangesAsync();
    }
    
    public async Task<Photo?> GetAsync(long id)
    {
        var photoEntity = await _context.Photos.FindAsync(id);
        
        return photoEntity != null ? EntityToModel(photoEntity) : null;
    }

    /// <summary>
    /// Of the given files of a photo source, the ones already saved for the user.
    /// </summary>
    public async Task<IReadOnlySet<string>> GetSavedExternalIdsAsync(long userId, long photoSourceId, IEnumerable<string> externalIds)
    {
        var ids = externalIds.ToArray();

        var savedExternalIds = await _context.Photos
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId && a.ExternalId != null &&
                        ids.Contains(a.ExternalId))
            .Select(a => a.ExternalId!)
            .ToListAsync();

        return savedExternalIds.ToHashSet();
    }

    public async Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, PhotoFilter filter, int top, int skip,
        PhotoSortOrder sortOrder)
    {
        var photos = GetUserPhotos(userId, filter);

        // the ID orders photos taken within the same second, which would otherwise be free to swap places
        // between one page and the next
        var sortedPhotos = sortOrder == PhotoSortOrder.Desc
            ? photos.OrderByDescending(a => a.DateTimeTaken).ThenByDescending(a => a.Id)
            : photos.OrderBy(a => a.DateTimeTaken).ThenBy(a => a.Id);

        var page = await sortedPhotos
            .Skip(skip)
            .Take(top)
            .ToListAsync();

        return page.Select(EntityToModel);
    }

    public async Task<int> GetTotalCountByUserIdAsync(long userId, PhotoFilter filter)
    {
        return await GetUserPhotos(userId, filter).CountAsync();
    }

    public async Task<IReadOnlyList<Photo>> GetGeotaggedAsync(long userId)
    {
        // every one of them at once, so the EXIF and the rest are left in the database
        return await _context.Photos
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.HasGps && a.DeletedOn == null)
            .OrderBy(a => a.DateTimeTaken)
            .ThenBy(a => a.Id)
            .Select(a => new Photo
            {
                Id = a.Id,
                UserId = a.UserId,
                FileName = a.FileName,
                Path = a.Path,
                DateTimeTaken = a.DateTimeTaken,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                HorizontalPositioningError = a.HorizontalPositioningError,
                HasGps = a.HasGps
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<int>> GetYearsAsync(long userId)
    {
        return await _context.Photos
            .Where(a => a.UserId == userId)
            .Select(a => a.DateTimeTaken.Year)
            .Distinct()
            .ToListAsync();
    }

    /// <summary>
    /// The photos not yet put in their categories by the given version of the rules, in the order they were saved.
    /// </summary>
    public async Task<IReadOnlyList<Photo>> GetNotCategorizedAsync(int version, int top)
    {
        var photos = await _context.Photos
            .Where(a => a.CategoriesVersion < version)
            .OrderBy(a => a.Id)
            .Take(top)
            .ToListAsync();

        return photos.Select(EntityToModel).ToList();
    }

    /// <summary>
    /// Replaces the categories of the photos, and records the version of the rules they were put in them by. The
    /// photos are all updated, or none of them is.
    /// </summary>
    public async Task SetCategoriesAsync(IReadOnlyDictionary<long, IReadOnlyCollection<PhotoCategory>> categoriesByPhotoId,
        int version)
    {
        var photoIds = categoriesByPhotoId.Keys.ToArray();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        await _context.PhotoCategories.Where(a => photoIds.Contains(a.PhotoId)).ExecuteDeleteAsync();

        await _context.PhotoCategories.AddRangeAsync(categoriesByPhotoId.SelectMany(a =>
            a.Value.Select(category => new PhotoCategoryEntity { PhotoId = a.Key, Category = category })));
        await _context.SaveChangesAsync();

        await _context.Photos
            .Where(a => photoIds.Contains(a.Id))
            .ExecuteUpdateAsync(a => a.SetProperty(photo => photo.CategoriesVersion, version));

        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<Photo>> GetDuplicatesAsync(long userId)
    {
        var photos = await _context.Photos
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.DuplicateGroupId != null)
            .ToListAsync();

        // the groups are ordered by their first photo, which the database cannot do without a join of its own
        return photos
            .GroupBy(a => a.DuplicateGroupId)
            .OrderBy(a => a.Min(b => b.DateTimeTaken))
            .ThenBy(a => a.Key)
            .SelectMany(a => a.OrderBy(b => b.Id))
            .Select(EntityToModel)
            .ToList();
    }

    public async Task<IReadOnlyList<PhotoDuplicateKey>> GetDuplicateKeysAsync()
    {
        return await _context.Photos
            .AsNoTracking()
            .Select(a => new PhotoDuplicateKey(a.Id, a.UserId, a.ContentHash, a.DeletedOn != null, a.DuplicateGroupId))
            .ToListAsync();
    }

    public async Task SetDuplicateGroupsAsync(IReadOnlyDictionary<long, long?> duplicateGroupIdsByPhotoId)
    {
        // a statement for each group rather than for each photo
        foreach (var group in duplicateGroupIdsByPhotoId.GroupBy(a => a.Value, a => a.Key))
        {
            var duplicateGroupId = group.Key;

            foreach (var photoIds in group.Chunk(1000))
            {
                await _context.Photos
                    .Where(a => photoIds.Contains(a.Id))
                    .ExecuteUpdateAsync(a => a.SetProperty(b => b.DuplicateGroupId, duplicateGroupId));
            }
        }
    }

    /// <summary>
    /// Marks a photo of the user as deleted, or no longer deleted when <paramref name="deletedOn"/> is null.
    /// </summary>
    /// <returns>false if the user has no such photo.</returns>
    public async Task<bool> SetDeletedOnAsync(long userId, long photoId, DateTimeOffset? deletedOn)
    {
        var updated = await _context.Photos
            .Where(a => a.UserId == userId && a.Id == photoId)
            .ExecuteUpdateAsync(a => a.SetProperty(photo => photo.DeletedOn, deletedOn));

        return updated > 0;
    }

    /// <summary>
    /// Deletes the photos a photo source was the origin of, for one user.
    /// </summary>
    /// <returns>The thumbnail files of the deleted photos, which the caller removes from the storage.</returns>
    public async Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId)
    {
        var photos = _context.Photos.Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId);

        var thumbnailPaths = await photos
            .Select(a => new { a.ThumbnailSmallFilePath, a.ThumbnailLargeFilePath })
            .ToListAsync();

        await photos.ExecuteDeleteAsync();

        return thumbnailPaths
            .SelectMany(a => new[] { a.ThumbnailSmallFilePath, a.ThumbnailLargeFilePath })
            .Where(a => !string.IsNullOrEmpty(a))
            .Select(a => a!)
            .ToList();
    }

    private IQueryable<PhotoEntity> GetUserPhotos(long userId, PhotoFilter filter)
    {
        var photos = _context.Photos.Where(a => a.UserId == userId);

        if (filter.PhotoSourceIds.Count > 0)
        {
            photos = photos.Where(a => filter.PhotoSourceIds.Contains(a.PhotoSourceId));
        }

        if (filter.Years.Count > 0)
        {
            photos = photos.Where(a => filter.Years.Contains(a.DateTimeTaken.Year));
        }

        if (filter.Categories.Count > 0)
        {
            var categories = filter.Categories
                .Where(a => a != PhotoCategory.Other && a != PhotoCategory.Deleted)
                .ToArray();
            var other = filter.Categories.Contains(PhotoCategory.Other);
            var deleted = filter.Categories.Contains(PhotoCategory.Deleted);

            // a photo marked as deleted is in the deleted ones only, whatever its other categories
            photos = photos.Where(a => (a.DeletedOn == null &&
                                        (a.Categories.Any(c => categories.Contains(c.Category)) ||
                                         (other && !a.Categories.Any()))) ||
                                       (deleted && a.DeletedOn != null));
        }
        else
        {
            photos = photos.Where(a => a.DeletedOn == null);
        }

        if (filter.HasGps != null)
        {
            photos = photos.Where(a => a.HasGps == filter.HasGps.Value);
        }

        return photos;
    }

    private static Photo EntityToModel(PhotoEntity photoEntity)
    {
        return new Photo
        {
            Id = photoEntity.Id,
            UserId = photoEntity.UserId,
            ThumbnailSmallFilePath = photoEntity.ThumbnailSmallFilePath,
            ThumbnailLargeFilePath = photoEntity.ThumbnailLargeFilePath,
            FileName = photoEntity.FileName,
            DateTimeTaken = photoEntity.DateTimeTaken,
            Latitude = photoEntity.Latitude,
            Longitude = photoEntity.Longitude,
            HorizontalPositioningError = photoEntity.HorizontalPositioningError,
            HasGps = photoEntity.HasGps,
            ExifString = photoEntity.ExifString,
            Path = photoEntity.Path,
            ExternalId = photoEntity.ExternalId,
            ContentHash = photoEntity.ContentHash,
            AddedOn = photoEntity.AddedOn,
            PhotoSourceId = photoEntity.PhotoSourceId,
            DeletedOn = photoEntity.DeletedOn,
            DuplicateGroupId = photoEntity.DuplicateGroupId
        };
    }

    private static PhotoEntity ModelToEntity(Photo photo)
    {
        return new PhotoEntity
        {
            UserId = photo.UserId,
            ThumbnailSmallFilePath = photo.ThumbnailSmallFilePath,
            ThumbnailLargeFilePath = photo.ThumbnailLargeFilePath,
            FileName = photo.FileName,
            DateTimeTaken = photo.DateTimeTaken,
            Latitude = photo.Latitude,
            Longitude = photo.Longitude,
            HorizontalPositioningError = photo.HorizontalPositioningError,
            HasGps = photo.HasGps,
            ExifString = photo.ExifString,
            Path = photo.Path,
            ExternalId = photo.ExternalId,
            ContentHash = photo.ContentHash,
            AddedOn = photo.AddedOn,
            PhotoSourceId = photo.PhotoSourceId
        };
    }
}
