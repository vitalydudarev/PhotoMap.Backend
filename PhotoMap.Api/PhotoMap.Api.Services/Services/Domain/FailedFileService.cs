using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Database;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain;

public class FailedFileService : IFailedFileService
{
    private readonly PhotoMapContext _context;
    private readonly IFileStorage _fileStorage;
    private readonly FailedPreviewsSignal _failedPreviewsSignal;
    private readonly ILogger<FailedFileService> _logger;

    public FailedFileService(PhotoMapContext context, IFileStorage fileStorage, FailedPreviewsSignal failedPreviewsSignal,
        ILogger<FailedFileService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _failedPreviewsSignal = failedPreviewsSignal;
        _logger = logger;
    }

    public Task<IReadOnlyCollection<FailedFile>> GetAsync(long userId, long photoSourceId)
    {
        return GetAsync(Photos(userId, photoSourceId, fromOtherUploads: false));
    }

    public Task<IReadOnlyCollection<FailedFile>> GetOtherUploadsAsync(long userId, long photoSourceId)
    {
        return GetAsync(Photos(userId, photoSourceId, fromOtherUploads: true));
    }

    public Task<IReadOnlyCollection<FailedFile>> GetVideosAsync(long userId, long photoSourceId)
    {
        return GetAsync(Videos(userId, photoSourceId));
    }

    public Task<int> CountAsync(long userId, long photoSourceId, bool fromOtherUploads)
    {
        return Photos(userId, photoSourceId, fromOtherUploads).CountAsync();
    }

    /// <summary>
    /// The failed camera uploads, or other uploads, of the photo source, without its failed videos.
    /// </summary>
    private IQueryable<FailedFileEntity> Photos(long userId, long photoSourceId, bool fromOtherUploads)
    {
        return _context.FailedFiles.Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId &&
                                               !a.FromVideos && a.FromOtherUploads == fromOtherUploads);
    }

    private IQueryable<FailedFileEntity> Videos(long userId, long photoSourceId)
    {
        return _context.FailedFiles.Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId && a.FromVideos);
    }

    private static async Task<IReadOnlyCollection<FailedFile>> GetAsync(IQueryable<FailedFileEntity> failedFiles)
    {
        return await failedFiles
            .OrderBy(a => a.Id)
            .AsNoTracking()
            .Select(a => new FailedFile
            {
                UserId = a.UserId,
                PhotoSourceId = a.PhotoSourceId,
                ExternalId = a.ExternalId,
                Path = a.Path,
                FileName = a.FileName,
                Stage = a.Stage,
                Error = a.Error,
                Attempts = a.Attempts,
                FailedAt = a.FailedAt,
                FromOtherUploads = a.FromOtherUploads,
                FromVideos = a.FromVideos
            })
            .ToListAsync();
    }

    /// <summary>
    /// The entities are read tracked and changed in place, in one save: a run records every page with the
    /// context of the run, which must not be left tracking a second instance of a row.
    /// </summary>
    public async Task RecordAsync(long userId, long photoSourceId, IReadOnlyCollection<FailedFile> failedFiles,
        IReadOnlyCollection<string> savedExternalIds)
    {
        if (failedFiles.Count == 0 && savedExternalIds.Count == 0)
        {
            return;
        }

        var externalIds = failedFiles.Select(a => a.ExternalId).Concat(savedExternalIds).Distinct().ToList();

        var entities = await _context.FailedFiles
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId && externalIds.Contains(a.ExternalId))
            .ToDictionaryAsync(a => a.ExternalId);

        var previewFilePaths = new List<string?>();

        foreach (var externalId in savedExternalIds)
        {
            if (entities.Remove(externalId, out var entity))
            {
                _context.FailedFiles.Remove(entity);
                previewFilePaths.Add(entity.PreviewFilePath);
            }
        }

        foreach (var failedFile in failedFiles)
        {
            if (!entities.TryGetValue(failedFile.ExternalId, out var entity))
            {
                entity = new FailedFileEntity
                {
                    UserId = userId,
                    PhotoSourceId = photoSourceId,
                    ExternalId = failedFile.ExternalId,
                    FileName = failedFile.FileName,
                    Error = failedFile.Error
                };

                _context.FailedFiles.Add(entity);
                entities.Add(failedFile.ExternalId, entity);
            }

            entity.Path = failedFile.Path;
            entity.FileName = failedFile.FileName;
            entity.Stage = failedFile.Stage;
            entity.Error = failedFile.Error;
            entity.Attempts++;
            entity.FailedAt = failedFile.FailedAt;
            entity.FromOtherUploads = failedFile.FromOtherUploads;
            entity.FromVideos = failedFile.FromVideos;
        }

        await _context.SaveChangesAsync();

        FailedPreviewFiles.Delete(_fileStorage, _logger, previewFilePaths);

        if (failedFiles.Count > 0)
        {
            _failedPreviewsSignal.Notify();
        }
    }

    public Task DeleteByPhotoSourceAsync(long userId, long photoSourceId)
    {
        return DeleteAsync(_context.FailedFiles.Where(a =>
            a.UserId == userId && a.PhotoSourceId == photoSourceId && !a.FromVideos));
    }

    public Task DeleteByPhotoSourceAsync(long userId, long photoSourceId, bool fromOtherUploads)
    {
        return DeleteAsync(Photos(userId, photoSourceId, fromOtherUploads));
    }

    public Task DeleteVideosByPhotoSourceAsync(long userId, long photoSourceId)
    {
        return DeleteAsync(Videos(userId, photoSourceId));
    }

    private async Task DeleteAsync(IQueryable<FailedFileEntity> failedFiles)
    {
        var entities = await failedFiles.ToListAsync();

        _context.FailedFiles.RemoveRange(entities);

        await _context.SaveChangesAsync();

        FailedPreviewFiles.Delete(_fileStorage, _logger, entities.Select(a => a.PreviewFilePath));
    }
}
