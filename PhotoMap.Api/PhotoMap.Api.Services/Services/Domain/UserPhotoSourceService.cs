using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Database;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain;

public class UserPhotoSourceService : IUserPhotoSourceService
{
    private readonly PhotoMapContext _context;

    public UserPhotoSourceService(PhotoMapContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<UserPhotoSource>> GetUserPhotoSourcesAsync(long userId)
    {
        var photoSourceEntities = await _context.PhotoSources
            .Include(a => a.UserPhotoSources.Where(b => b.UserId == userId))
            .ToListAsync();

        return photoSourceEntities.Select(a => new UserPhotoSource
        {
            UserId = userId,
            PhotoSourceId = a.Id,
            PhotoSourceName = a.Name,
            IsUserAuthorized = a.UserPhotoSources.FirstOrDefault()?.UserAuthSettings?.TokenExpiresOn > DateTime.UtcNow,
            TokenExpiresOn = a.UserPhotoSources.FirstOrDefault()?.UserAuthSettings?.TokenExpiresOn.UtcDateTime
        });
    }
    
    public async Task<UserAuthSettings?> GetUserAuthSettingsAsync(long userId, long photoSourceId)
    {
        var userPhotoSourceEntity = await _context.UserPhotoSources
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId)
            .Include(a => a.UserAuthSettings)
            .FirstOrDefaultAsync();

        if (userPhotoSourceEntity != null)
        {
            return userPhotoSourceEntity.UserAuthSettings;
        }

        throw new NotFoundException($"UserPhotoSource entity for user ID {userId} and photo source ID {photoSourceId} not found.");
    }
    
    public async Task UpdateUserPhotoSourceAuthResultAsync(long userId, long photoSourceId, UserAuthSettings userAuthSettings)
    {
        var userPhotoSource = await _context.UserPhotoSources.FirstOrDefaultAsync(a => a.UserId == userId && a.PhotoSourceId == photoSourceId);

        if (userPhotoSource == null)
        {
            var entity = new UserPhotoSourceEntity { UserId = userId, PhotoSourceId = photoSourceId, UserAuthSettings = userAuthSettings };
            await _context.UserPhotoSources.AddAsync(entity);
        }
        else
        {
            userPhotoSource.UserAuthSettings = userAuthSettings;
            _context.UserPhotoSources.Update(userPhotoSource);
        }

        await _context.SaveChangesAsync();
    }
}