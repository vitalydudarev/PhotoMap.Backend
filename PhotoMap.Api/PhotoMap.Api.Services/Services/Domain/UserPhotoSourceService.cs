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

        var statuses = await _context.UserPhotoSourcesStatuses
            .Where(a => a.UserId == userId)
            .AsNoTracking()
            .ToDictionaryAsync(a => a.PhotoSourceId, a => a.Status);
            
        return userPhotoSourceEntities.Select(a => new Api.Domain.Models.UserPhotoSource
        {
            Status = statuses.GetValueOrDefault(a.PhotoSourceId, PhotoSourceStatus.NotStarted),
            UserId = userId,
            PhotoSourceId = a.PhotoSource!.Id,
            PhotoSourceName = a.PhotoSource!.Name,
            IsUserAuthorized = a.UserAuthResult is { IsValid: true },
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
            .FirstOrDefaultAsync(a => a.UserId == userId && a.PhotoSourceId == photoSourceId);
        
        if (userPhotoSource == null)
        {
            throw new NotFoundException($"UserPhotoSource entity for user ID {userId} and photo source ID {photoSourceId} not found.");
        }
        
        userPhotoSource.UserAuthResult = userAuthResult;

        await _context.SaveChangesAsync();
    }
    
    /// <summary>
    /// The entity is read tracked and changed in place: a run saves its state after every page, and attaching a
    /// second instance of it to the context of the run would fail on the key the first one is tracked by.
    /// </summary>
    public async Task UpdateUserPhotoStateAsync(long userId, long photoSourceId, string? state)
    {
        var entity = await _context.UserPhotoSources
            .FirstOrDefaultAsync(a => a.UserId == userId && a.PhotoSourceId == photoSourceId);
        
        if (entity != null)
        {
            entity.ProcessingState = state;

            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteUserPhotoStatusAsync(long userId, long photoSourceId)
    {
        var entity = await _context.UserPhotoSourcesStatuses
            .FirstOrDefaultAsync(a => a.UserId == userId && a.PhotoSourceId == photoSourceId);

        if (entity != null)
        {
            _context.UserPhotoSourcesStatuses.Remove(entity);

            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateUserPhotoStatusAsync(UserPhotoSourceStatus status)
    {
        var entity = await _context.UserPhotoSourcesStatuses
            .FirstOrDefaultAsync(a => a.UserId == status.UserId && a.PhotoSourceId == status.PhotoSourceId);

        if (entity == null)
        {
            entity = new UserPhotoSourceStatusEntity { UserId = status.UserId, PhotoSourceId = status.PhotoSourceId };
            _context.UserPhotoSourcesStatuses.Add(entity);
        }

        entity.Status = status.Status;
        entity.TotalCount = status.TotalCount;
        entity.ProcessedCount = status.ProcessedCount;
        entity.FailedCount = status.FailedCount;
        entity.LastUpdatedAt = status.LastUpdatedAt;

        await _context.SaveChangesAsync();
    }

    public async Task<int> PauseInProgressAsync()
    {
        var entities = await _context.UserPhotoSourcesStatuses
            .Where(a => a.Status == PhotoSourceStatus.InProgress)
            .ToListAsync();

        foreach (var entity in entities)
        {
            entity.Status = PhotoSourceStatus.Paused;
            entity.LastUpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();

        return entities.Count;
    }
}