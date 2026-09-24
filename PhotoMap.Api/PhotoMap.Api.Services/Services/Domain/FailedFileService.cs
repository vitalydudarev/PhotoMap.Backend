using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Database;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain;

public class FailedFileService : IFailedFileService
{
    private readonly PhotoMapContext _context;

    public FailedFileService(PhotoMapContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<FailedFile>> GetAsync(long userId, long photoSourceId)
    {
        return await _context.FailedFiles
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId)
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
                FailedAt = a.FailedAt
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

        foreach (var externalId in savedExternalIds)
        {
            if (entities.Remove(externalId, out var entity))
            {
                _context.FailedFiles.Remove(entity);
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
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteByPhotoSourceAsync(long userId, long photoSourceId)
    {
        var entities = await _context.FailedFiles
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId)
            .ToListAsync();

        _context.FailedFiles.RemoveRange(entities);

        await _context.SaveChangesAsync();
    }
}
