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
    
    public async Task AddAsync(Photo photo)
    {
        var photoEntity = ModelToEntity(photo);
        
        await _context.Photos.AddAsync(photoEntity);
        await _context.SaveChangesAsync();
    }
    
    public async Task<Photo?> GetAsync(long id)
    {
        var photoEntity = await _context.Photos.FindAsync(id);
        
        return photoEntity != null ? EntityToModel(photoEntity) : null;
    }

    public async Task<Photo?> GetByFileNameAsync(string fileName)
    {
        var photoEntity = await _context.Photos.FirstOrDefaultAsync(a => a.FileName == fileName);
        
        return photoEntity != null ? EntityToModel(photoEntity) : null;
    }

    public Task<bool> ExistsAsync(long userId, long photoSourceId, string externalId)
    {
        return _context.Photos.AnyAsync(a =>
            a.UserId == userId && a.PhotoSourceId == photoSourceId && a.ExternalId == externalId);
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

    public async Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, int top, int skip)
    {
        var photos = await _context.Photos
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.DateTimeTaken)
            .Skip(skip)
            .Take(top)
            .ToListAsync();

        return photos.Select(EntityToModel);
    }

    public async Task<int> GetTotalCountByUserIdAsync(long userId)
    {
        return await _context.Photos.CountAsync(a => a.UserId == userId);
    }

    public async Task DeleteByUserIdAsync(long userId)
    {
        var entities = await _context.Photos.Where(a => a.UserId == userId).ToListAsync();
        _context.Photos.RemoveRange(entities);
        
        await _context.SaveChangesAsync();
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
