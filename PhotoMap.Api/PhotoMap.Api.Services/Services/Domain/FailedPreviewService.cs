using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Database;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain;

public class FailedPreviewService : IFailedPreviewService
{
    private static readonly Expression<Func<FailedFileEntity, FailedPreview>> FromFailedFile = a => new FailedPreview
    {
        Table = FailedPreviewTable.FailedFiles,
        Id = a.Id,
        UserId = a.UserId,
        PhotoSourceId = a.PhotoSourceId,
        Stage = a.Stage,
        FileName = a.FileName,
        // Yandex.Disk finds a file by its path only, Dropbox by its path as well
        Reference = a.Path ?? a.ExternalId,
        Error = a.Error,
        Attempts = a.Attempts,
        FailedAt = a.FailedAt,
        PreviewFilePath = a.PreviewFilePath,
        PreviewContentType = a.PreviewContentType,
        PreviewError = a.PreviewError,
        PreviewAttempts = a.PreviewAttempts,
        PreviewAttemptedAt = a.PreviewAttemptedAt
    };

    private static readonly Expression<Func<FailedDownloadEntity, FailedPreview>> FromFailedDownload = a => new FailedPreview
    {
        Table = FailedPreviewTable.FailedDownloads,
        Id = a.Id,
        UserId = a.UserId,
        PhotoSourceId = a.PhotoSourceId,
        DownloadKind = a.Kind,
        FileName = a.FileName,
        Folder = a.Folder == "" ? null : a.Folder,
        Reference = a.Path,
        Error = a.Error,
        Attempts = a.Attempts,
        FailedAt = a.FailedAt,
        PreviewFilePath = a.PreviewFilePath,
        PreviewContentType = a.PreviewContentType,
        PreviewError = a.PreviewError,
        PreviewAttempts = a.PreviewAttempts,
        PreviewAttemptedAt = a.PreviewAttemptedAt
    };

    private readonly PhotoMapContext _context;

    public FailedPreviewService(PhotoMapContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<FailedPreview>> GetAsync(long userId, long photoSourceId)
    {
        var failedFiles = await _context.FailedFiles.AsNoTracking()
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId)
            .Select(FromFailedFile)
            .ToListAsync();
        var failedDownloads = await _context.FailedDownloads.AsNoTracking()
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId)
            .Select(FromFailedDownload)
            .ToListAsync();

        return failedFiles.Concat(failedDownloads).OrderByDescending(a => a.FailedAt).ToList();
    }

    public Task<FailedPreview?> GetAsync(FailedPreviewTable table, long id)
    {
        return table == FailedPreviewTable.FailedFiles
            ? _context.FailedFiles.AsNoTracking().Where(a => a.Id == id).Select(FromFailedFile).FirstOrDefaultAsync()
            : _context.FailedDownloads.AsNoTracking().Where(a => a.Id == id).Select(FromFailedDownload).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<FailedPreview>> GetPendingAsync(int maxAttempts, DateTimeOffset attemptedBefore, int limit)
    {
        var failedFiles = await _context.FailedFiles.AsNoTracking()
            .Where(a => a.PreviewFilePath == null && a.PreviewAttempts < maxAttempts &&
                        (a.PreviewAttemptedAt == null || a.PreviewAttemptedAt < attemptedBefore))
            .OrderBy(a => a.Id)
            .Take(limit)
            .Select(FromFailedFile)
            .ToListAsync();
        var failedDownloads = await _context.FailedDownloads.AsNoTracking()
            .Where(a => a.PreviewFilePath == null && a.PreviewAttempts < maxAttempts &&
                        (a.PreviewAttemptedAt == null || a.PreviewAttemptedAt < attemptedBefore))
            .OrderBy(a => a.Id)
            .Take(limit)
            .Select(FromFailedDownload)
            .ToListAsync();

        return failedFiles.Concat(failedDownloads).ToList();
    }

    /// <summary>
    /// Updated in the database directly: the run that has recorded the failure may be changing the same row, it
    /// changes the failure, not the preview.
    /// </summary>
    public async Task<bool> SavePreviewAsync(FailedPreviewTable table, long id, string previewFilePath, string contentType)
    {
        var now = DateTimeOffset.UtcNow;

        var updated = table == FailedPreviewTable.FailedFiles
            ? await _context.FailedFiles.Where(a => a.Id == id).ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.PreviewFilePath, previewFilePath)
                .SetProperty(a => a.PreviewContentType, contentType)
                .SetProperty(a => a.PreviewError, (string?)null)
                .SetProperty(a => a.PreviewAttempts, a => a.PreviewAttempts + 1)
                .SetProperty(a => a.PreviewAttemptedAt, now))
            : await _context.FailedDownloads.Where(a => a.Id == id).ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.PreviewFilePath, previewFilePath)
                .SetProperty(a => a.PreviewContentType, contentType)
                .SetProperty(a => a.PreviewError, (string?)null)
                .SetProperty(a => a.PreviewAttempts, a => a.PreviewAttempts + 1)
                .SetProperty(a => a.PreviewAttemptedAt, now));

        return updated > 0;
    }

    public async Task PreviewFailedAsync(FailedPreviewTable table, long id, string error, bool countAttempt)
    {
        var now = DateTimeOffset.UtcNow;
        var attempt = countAttempt ? 1 : 0;

        if (table == FailedPreviewTable.FailedFiles)
        {
            await _context.FailedFiles.Where(a => a.Id == id).ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.PreviewError, error)
                .SetProperty(a => a.PreviewAttempts, a => a.PreviewAttempts + attempt)
                .SetProperty(a => a.PreviewAttemptedAt, now));
        }
        else
        {
            await _context.FailedDownloads.Where(a => a.Id == id).ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.PreviewError, error)
                .SetProperty(a => a.PreviewAttempts, a => a.PreviewAttempts + attempt)
                .SetProperty(a => a.PreviewAttemptedAt, now));
        }
    }

    public async Task<int> ResetAsync(long userId, long photoSourceId)
    {
        var failedFiles = await _context.FailedFiles
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId && a.PreviewFilePath == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.PreviewAttempts, 0)
                .SetProperty(a => a.PreviewAttemptedAt, (DateTimeOffset?)null));
        var failedDownloads = await _context.FailedDownloads
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId && a.PreviewFilePath == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.PreviewAttempts, 0)
                .SetProperty(a => a.PreviewAttemptedAt, (DateTimeOffset?)null));

        return failedFiles + failedDownloads;
    }
}
