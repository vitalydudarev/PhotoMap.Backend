using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Database;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain;

public class VideoService : IVideoService
{
    private readonly PhotoMapContext _context;

    public VideoService(PhotoMapContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Saves the videos not saved yet: one already saved, or given twice, is saved once, rather than fail the
    /// others on the unique index of the IDs the photo source assigned.
    /// </summary>
    public async Task AddRangeAsync(IReadOnlyCollection<Video> videos)
    {
        var videosToSave = new List<Video>();

        foreach (var sourceVideos in videos.GroupBy(a => (a.UserId, a.PhotoSourceId)))
        {
            var (userId, photoSourceId) = sourceVideos.Key;
            var savedExternalIds = await GetSavedExternalIdsAsync(userId, photoSourceId, sourceVideos.Select(a => a.ExternalId));

            videosToSave.AddRange(sourceVideos
                .Where(a => !savedExternalIds.Contains(a.ExternalId))
                .DistinctBy(a => a.ExternalId));
        }

        if (videosToSave.Count == 0)
        {
            return;
        }

        var entities = videosToSave.Select(a => new VideoEntity
        {
            UserId = a.UserId,
            PhotoSourceId = a.PhotoSourceId,
            ExternalId = a.ExternalId,
            FileName = a.FileName,
            FolderPath = a.FolderPath,
            MimeType = a.MimeType,
            Size = a.Size,
            // timestamptz columns only accept UTC
            DateTimeTaken = a.DateTimeTaken.ToUniversalTime(),
            ExifDateTime = a.ExifDateTime?.ToUniversalTime(),
            Latitude = a.Latitude,
            Longitude = a.Longitude,
            PreviewFilePath = a.PreviewFilePath,
            PreviewContentType = a.PreviewContentType,
            AddedOn = a.AddedOn.ToUniversalTime()
        }).ToList();

        _context.Videos.AddRange(entities);

        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            // a run saves page after page with one context, which must not keep tracking every video it has saved,
            // nor, when the save has failed, try to save them again with the next page
            foreach (var entity in entities)
            {
                _context.Entry(entity).State = EntityState.Detached;
            }
        }
    }

    public async Task<Video?> GetAsync(long id)
    {
        var entity = await _context.Videos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);

        return entity == null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<Video>> GetByUserIdAsync(long userId, IReadOnlyCollection<string> folderPaths, int top,
        int skip, PhotoSortOrder sortOrder)
    {
        var videos = GetUserVideos(userId, folderPaths).AsNoTracking();

        var ordered = sortOrder == PhotoSortOrder.Desc
            ? videos.OrderByDescending(a => a.DateTimeTaken).ThenByDescending(a => a.Id)
            : videos.OrderBy(a => a.DateTimeTaken).ThenBy(a => a.Id);

        var entities = await ordered.Skip(skip).Take(top).ToListAsync();

        return entities.Select(ToModel).ToList();
    }

    public Task<int> GetTotalCountByUserIdAsync(long userId, IReadOnlyCollection<string> folderPaths)
    {
        return GetUserVideos(userId, folderPaths).CountAsync();
    }

    public async Task<IReadOnlyList<string>> GetFolderPathsAsync(long userId)
    {
        return await _context.Videos
            .Where(a => a.UserId == userId && a.FolderPath != null)
            .Select(a => a.FolderPath!)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
    }

    public async Task<IReadOnlySet<string>> GetSavedExternalIdsAsync(long userId, long photoSourceId,
        IEnumerable<string> externalIds)
    {
        var ids = externalIds.ToList();
        if (ids.Count == 0)
        {
            return new HashSet<string>();
        }

        var savedIds = await _context.Videos
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId && ids.Contains(a.ExternalId))
            .Select(a => a.ExternalId)
            .ToListAsync();

        return savedIds.ToHashSet();
    }

    public async Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId)
    {
        var videos = _context.Videos.Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId);

        var previewPaths = await videos
            .Where(a => a.PreviewFilePath != null)
            .Select(a => a.PreviewFilePath!)
            .ToListAsync();

        await videos.ExecuteDeleteAsync();

        return previewPaths;
    }

    public async Task<UserPhotoSourceStatus?> GetStatusAsync(long userId, long photoSourceId)
    {
        var entity = await _context.UserVideoSourcesStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.PhotoSourceId == photoSourceId);

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

    public async Task UpdateStatusAsync(UserPhotoSourceStatus status)
    {
        var entity = await _context.UserVideoSourcesStatuses
            .FirstOrDefaultAsync(a => a.UserId == status.UserId && a.PhotoSourceId == status.PhotoSourceId);

        if (entity == null)
        {
            entity = new UserVideoSourceStatusEntity { UserId = status.UserId, PhotoSourceId = status.PhotoSourceId };
            _context.UserVideoSourcesStatuses.Add(entity);
        }

        entity.Status = status.Status;
        entity.TotalCount = status.TotalCount;
        entity.ProcessedCount = status.ProcessedCount;
        entity.FailedCount = status.FailedCount;
        entity.LastUpdatedAt = status.LastUpdatedAt;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteStatusAsync(long userId, long photoSourceId)
    {
        await _context.UserVideoSourcesStatuses
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId)
            .ExecuteDeleteAsync();
    }

    public async Task<int> PauseInProgressAsync()
    {
        var entities = await _context.UserVideoSourcesStatuses
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

    private IQueryable<VideoEntity> GetUserVideos(long userId, IReadOnlyCollection<string> folderPaths)
    {
        var videos = _context.Videos.Where(a => a.UserId == userId);

        return folderPaths.Count > 0 ? videos.Where(a => a.FolderPath != null && folderPaths.Contains(a.FolderPath)) : videos;
    }

    private static Video ToModel(VideoEntity entity)
    {
        return new Video
        {
            Id = entity.Id,
            UserId = entity.UserId,
            PhotoSourceId = entity.PhotoSourceId,
            ExternalId = entity.ExternalId,
            FileName = entity.FileName,
            FolderPath = entity.FolderPath,
            MimeType = entity.MimeType,
            Size = entity.Size,
            DateTimeTaken = entity.DateTimeTaken,
            ExifDateTime = entity.ExifDateTime,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            PreviewFilePath = entity.PreviewFilePath,
            PreviewContentType = entity.PreviewContentType,
            AddedOn = entity.AddedOn
        };
    }
}
