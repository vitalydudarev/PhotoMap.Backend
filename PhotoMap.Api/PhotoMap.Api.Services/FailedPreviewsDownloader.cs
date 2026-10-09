using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

/// <summary>
/// Downloads the previews of the files that failed, of the processing of the photos and of the downloads alike, so
/// that they show which files they are. A preview that cannot be downloaded is tried again later, a few times.
/// </summary>
public class FailedPreviewsDownloader
{
    /// <summary>
    /// How many times a preview is tried before it is given up on, until the user has it tried again.
    /// </summary>
    public const int MaxPreviewAttempts = 5;

    /// <summary>
    /// How long a preview that could not be downloaded waits to be tried again: Yandex.Disk may not have made it yet.
    /// </summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(1);

    private const int BatchSize = 100;
    private const int MaxParallelDownloads = 4;

    private readonly IFailedPreviewService _failedPreviewService;
    private readonly IPhotoSourceService _photoSourceService;
    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly ISourceFilesFactory _sourceFilesFactory;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<FailedPreviewsDownloader> _logger;

    public FailedPreviewsDownloader(
        IFailedPreviewService failedPreviewService,
        IPhotoSourceService photoSourceService,
        IUserPhotoSourceService userPhotoSourceService,
        ISourceFilesFactory sourceFilesFactory,
        IFileStorage fileStorage,
        ILogger<FailedPreviewsDownloader> logger)
    {
        _failedPreviewService = failedPreviewService;
        _photoSourceService = photoSourceService;
        _userPhotoSourceService = userPhotoSourceService;
        _sourceFilesFactory = sourceFilesFactory;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    /// <summary>
    /// Downloads the previews still to download, a batch at a time, until every one has been tried.
    /// </summary>
    /// <returns>How many previews have been downloaded.</returns>
    public async Task<int> DownloadPendingAsync(CancellationToken cancellationToken)
    {
        var downloadedCount = 0;

        while (true)
        {
            // a preview tried is not pending again until RetryAfter, so the rounds come to an end
            var pending = await _failedPreviewService.GetPendingAsync(MaxPreviewAttempts, DateTimeOffset.UtcNow - RetryAfter,
                BatchSize);
            if (pending.Count == 0)
            {
                return downloadedCount;
            }

            foreach (var group in pending.GroupBy(a => (a.UserId, a.PhotoSourceId)))
            {
                downloadedCount += await DownloadAsync(group.Key.UserId, group.Key.PhotoSourceId, group.ToList(), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Downloads the previews of the failed files of a source of a user, several at a time. The outcomes are recorded
    /// once all of them have been downloaded, one at a time: the database context is not thread safe.
    /// </summary>
    private async Task<int> DownloadAsync(long userId, long sourceId, IReadOnlyCollection<FailedPreview> failedPreviews,
        CancellationToken cancellationToken)
    {
        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);
        var authResult = await _userPhotoSourceService.GetAuthResultAsync(userId, sourceId);

        if (!_sourceFilesFactory.Supports(photoSource) || authResult == null || !authResult.IsValid)
        {
            // tried again once the user has authorized, the files themselves are not at fault
            await PreviewsFailedAsync(failedPreviews, $"{photoSource.Name} is not authorized.", countAttempt: false);

            return 0;
        }

        using var sourceFiles = _sourceFilesFactory.Create(photoSource, authResult);

        var outcomes = new ConcurrentBag<(FailedPreview FailedPreview, SourcePreview? Preview, Exception? Error)>();
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDownloads, CancellationToken = cancellationToken };

        await Parallel.ForEachAsync(failedPreviews, parallelOptions, async (failedPreview, token) =>
        {
            try
            {
                outcomes.Add((failedPreview, await sourceFiles.DownloadPreviewAsync(failedPreview.Reference, token), null));
            }
            catch (Exception e) when (e is not OperationCanceledException || !token.IsCancellationRequested)
            {
                outcomes.Add((failedPreview, null, e));
            }
        });

        var downloadedCount = 0;

        foreach (var (failedPreview, preview, error) in outcomes)
        {
            if (error != null)
            {
                var isAuthError = sourceFiles.IsAuthError(error);

                if (!isAuthError)
                {
                    _logger.LogWarning(error, "Failed to download the preview of {FileName}", failedPreview.FileName);
                }

                await _failedPreviewService.PreviewFailedAsync(failedPreview.Table, failedPreview.Id,
                    isAuthError ? $"{photoSource.Name} no longer accepts the access token." : error.Message, countAttempt: !isAuthError);
            }
            else if (preview == null)
            {
                await _failedPreviewService.PreviewFailedAsync(failedPreview.Table, failedPreview.Id,
                    $"{photoSource.Name} has no preview of the file.", countAttempt: true);
            }
            else if (await SaveAsync(photoSource.Name, failedPreview, preview))
            {
                downloadedCount++;
            }
        }

        _logger.LogInformation("Downloaded {DownloadedCount} of {FileCount} previews of the failed files of {PhotoSourceName} for user {UserId}",
            downloadedCount, failedPreviews.Count, photoSource.Name, userId);

        return downloadedCount;
    }

    /// <returns>false when the file no longer fails, its preview is deleted again.</returns>
    private async Task<bool> SaveAsync(string photoSourceName, FailedPreview failedPreview, SourcePreview preview)
    {
        var previewFilePath = FailedPreviewFiles.GetPath(photoSourceName, failedPreview, preview.ContentType);

        try
        {
            await _fileStorage.SaveAsync(previewFilePath, preview.Contents);
        }
        catch (IOException e)
        {
            _logger.LogError(e, "Failed to store the preview of {FileName}", failedPreview.FileName);

            await _failedPreviewService.PreviewFailedAsync(failedPreview.Table, failedPreview.Id,
                "The preview could not be stored: " + e.Message, countAttempt: true);

            return false;
        }

        if (await _failedPreviewService.SavePreviewAsync(failedPreview.Table, failedPreview.Id, previewFilePath, preview.ContentType))
        {
            return true;
        }

        FailedPreviewFiles.Delete(_fileStorage, _logger, [previewFilePath]);

        return false;
    }

    private async Task PreviewsFailedAsync(IEnumerable<FailedPreview> failedPreviews, string error, bool countAttempt)
    {
        foreach (var failedPreview in failedPreviews)
        {
            await _failedPreviewService.PreviewFailedAsync(failedPreview.Table, failedPreview.Id, error, countAttempt);
        }
    }
}
