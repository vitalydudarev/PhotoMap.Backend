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
            AuthSettings = a.UserPhotoSources.FirstOrDefault()?.AuthSettings,
            IsUserAuthorized = a.UserPhotoSources.FirstOrDefault()?.AuthSettings?.TokenExpiresOn > DateTime.UtcNow
        });
    }
    
    public async Task UpdateUserPhotoSourceAuthResultAsync(long userId, long photoSourceId, AuthResult authResult)
    {
        var userPhotoSource = await _context.UserPhotoSources.FirstOrDefaultAsync(a => a.UserId == userId && a.PhotoSourceId == photoSourceId);

        if (userPhotoSource == null)
        {
            var entity = new UserPhotoSourceEntity { UserId = userId, PhotoSourceId = photoSourceId, AuthSettings = authResult };
            await _context.UserPhotoSources.AddAsync(entity);
        }
        else
        {
            userPhotoSource.AuthSettings = authResult;
            _context.UserPhotoSources.Update(userPhotoSource);
        }

        await _context.SaveChangesAsync();
    }
}