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

    public async Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, int top, int skip, PhotoSortOrder sortOrder)
    {
        var photos = _context.Photos.Where(a => a.UserId == userId);

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

    public async Task<int> GetTotalCountByUserIdAsync(long userId)
    {
        return await _context.Photos.CountAsync(a => a.UserId == userId);
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

    public async Task DeleteAllAsync()
    {
        var entities = await _context.Photos.ToListAsync();
        _context.Photos.RemoveRange(entities);

        await _context.SaveChangesAsync();
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
            HasGps = photoEntity.HasGps,
            ExifString = photoEntity.ExifString,
            Path = photoEntity.Path,
            ExternalId = photoEntity.ExternalId,
            ContentHash = photoEntity.ContentHash,
            AddedOn = photoEntity.AddedOn,
            PhotoSourceId = photoEntity.PhotoSourceId
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
