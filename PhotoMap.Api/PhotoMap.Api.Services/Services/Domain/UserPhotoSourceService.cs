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

    public async Task<AuthResult?> GetAuthSettings(long userId, long photoSourceId)
    {
        var userPhotoSource = await _context.UserPhotoSources.FirstOrDefaultAsync(a => a.UserId == userId && a.PhotoSourceId == photoSourceId);
        
        return userPhotoSource?.AuthSettings;
    }
    
    public async Task UpdateAuthSettings(long userId, long photoSourceId, AuthResult authResult)
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