using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Database;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;

namespace PhotoMap.Api.Services.Services.Domain;

public class UserPhotoSourceService : IUserPhotoSourceService
{
    private readonly PhotoMapContext _context;

    public UserPhotoSourceService(PhotoMapContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Api.Domain.Models.UserPhotoSource>> GetUserPhotoSourcesAsync(long userId)
    {
        var userPhotoSourceEntities = await _context.UserPhotoSources
            .Where(a => a.UserId == userId)
            .Include(userPhotoSource => userPhotoSource.PhotoSource!)
            .AsNoTracking()
            .ToListAsync();
            
        return userPhotoSourceEntities.Select(a => new Api.Domain.Models.UserPhotoSource
        {
            UserId = userId,
            PhotoSourceId = a.PhotoSource!.Id,
            PhotoSourceName = a.PhotoSource!.Name,
            IsUserAuthorized = a.UserAuthResult?.TokenExpiresOn > DateTime.UtcNow,
            TokenExpiresOn = a.UserAuthResult?.TokenExpiresOn.UtcDateTime
        });
    }
    
    public async Task<UserAuthResult?> GetAuthResultAsync(long userId, long photoSourceId)
    {
        var userPhotoSource = await _context.UserPhotoSources
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (userPhotoSource != null)
        {
            return userPhotoSource.UserAuthResult;
        }

        throw new NotFoundException($"UserPhotoSource entity for user ID {userId} and photo source ID {photoSourceId} not found.");
    }
    
    public async Task<UserPhotoSourceStatus?> GetUserPhotoStatusAsync(long userId, long photoSourceId)
    {
        var entity = await _context.UserPhotoSourcesStatuses
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return null;
        }

        return new UserPhotoSourceStatus
        {
            UserId = entity.UserId,
            PhotoSourceId = entity.PhotoSourceId,
            Status = entity.Status,
            TotalCount = entity.TotalCount,
            ProcessedCount = entity.ProcessedCount,
            FailedCount = entity.FailedCount,
            LastUpdatedAt = entity.LastUpdatedAt
        };
    }
    
    public async Task<UserPhotoSourceState?> GetUserPhotoStateAsync(long userId, long photoSourceId)
    {
        var entity = await _context.UserPhotoSources
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return null;
        }

        return new UserPhotoSourceState
        {
            UserId = entity.UserId,
            PhotoSourceId = entity.PhotoSourceId,
            State = entity.ProcessingState
        };
    }
    
    public async Task UpdateAuthResultAsync(long userId, long photoSourceId, UserAuthResult userAuthResult)
    {
        var userPhotoSource = await _context.UserPhotoSources
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        
        if (userPhotoSource == null)
        {
            throw new NotFoundException($"UserPhotoSource entity for user ID {userId} and photo source ID {photoSourceId} not found.");
        }
        
        userPhotoSource.UserAuthResult = userAuthResult;
        
        _context.UserPhotoSources.Update(userPhotoSource);

        await _context.SaveChangesAsync();
    }
    
    public async Task UpdateUserPhotoStateAsync(long userId, long photoSourceId, string state)
    {
        var entity = await _context.UserPhotoSources
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        
        if (entity != null)
        {
            entity.ProcessingState = state;

            _context.UserPhotoSources.Update(entity);

            await _context.SaveChangesAsync();
        }
    }
}