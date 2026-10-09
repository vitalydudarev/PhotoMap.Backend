using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Database;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain;

public class FailedDownloadService : IFailedDownloadService
{
    private readonly PhotoMapContext _context;
    private readonly IFileStorage _fileStorage;
    private readonly FailedPreviewsSignal _failedPreviewsSignal;
    private readonly ILogger<FailedDownloadService> _logger;

    public FailedDownloadService(PhotoMapContext context, IFileStorage fileStorage, FailedPreviewsSignal failedPreviewsSignal,
        ILogger<FailedDownloadService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _failedPreviewsSignal = failedPreviewsSignal;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FailedDownload>> GetAsync(long userId, long photoSourceId, SourceDownloadKind kind)
    {
        return await _context.FailedDownloads
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId && a.Kind == kind)
            .OrderBy(a => a.Id)
            .AsNoTracking()
            .Select(a => new FailedDownload
            {
                UserId = a.UserId,
                PhotoSourceId = a.PhotoSourceId,
                Kind = a.Kind,
                Folder = a.Folder,
                FileName = a.FileName,
                Path = a.Path,
                Size = a.Size,
                Content = a.Content,
                Error = a.Error,
                Attempts = a.Attempts,
                FailedAt = a.FailedAt
            })
            .ToListAsync();
    }

    /// <summary>
    /// The entities are read tracked and changed in place, in one save: a download records its outcomes with the
    /// context of the run, which must not be left tracking a second instance of a row.
    /// </summary>
    public async Task RecordAsync(long userId, long photoSourceId, SourceDownloadKind kind,
        IReadOnlyCollection<FailedDownload> failedDownloads, IReadOnlyCollection<DownloadedFileKey> downloadedFiles)
    {
        if (failedDownloads.Count == 0 && downloadedFiles.Count == 0)
        {
            return;
        }

        // looked up by name, the folders of the files are told apart once they have been read
        var fileNames = failedDownloads.Select(a => a.FileName).Concat(downloadedFiles.Select(a => a.FileName)).Distinct().ToList();

        var entities = (await _context.FailedDownloads
                .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId && a.Kind == kind && fileNames.Contains(a.FileName))
                .ToListAsync())
            .ToDictionary(a => new DownloadedFileKey(a.Folder, a.FileName));

        var previewFilePaths = new List<string?>();

        foreach (var key in downloadedFiles)
        {
            if (entities.Remove(key, out var entity))
            {
                _context.FailedDownloads.Remove(entity);
                previewFilePaths.Add(entity.PreviewFilePath);
            }
        }

        foreach (var failedDownload in failedDownloads)
        {
            var key = new DownloadedFileKey(failedDownload.Folder, failedDownload.FileName);

            if (!entities.TryGetValue(key, out var entity))
            {
                entity = new FailedDownloadEntity
                {
                    UserId = userId,
                    PhotoSourceId = photoSourceId,
                    Kind = kind,
                    Folder = failedDownload.Folder,
                    FileName = failedDownload.FileName,
                    Path = failedDownload.Path,
                    Error = failedDownload.Error
                };

                _context.FailedDownloads.Add(entity);
                entities.Add(key, entity);
            }

            entity.Path = failedDownload.Path;
            entity.Size = failedDownload.Size;
            entity.Content = failedDownload.Content;
            entity.Error = failedDownload.Error;
            entity.Attempts++;
            entity.FailedAt = failedDownload.FailedAt;
        }

        await _context.SaveChangesAsync();

        FailedPreviewFiles.Delete(_fileStorage, _logger, previewFilePaths);

        if (failedDownloads.Count > 0)
        {
            _failedPreviewsSignal.Notify();
        }
    }
}
