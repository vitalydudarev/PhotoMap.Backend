using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services;

public class VideoProcessingService : IVideoProcessingService
{
    private static readonly TimeSpan StatusReportInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How many videos have their previews downloaded before they are saved, which is how much of a stopped run
    /// is downloaded again when it resumes.
    /// </summary>
    private const int SaveEvery = 100;

    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly IPhotoSourceService _photoSourceService;
    private readonly IVideoService _videoService;
    private readonly IFailedFileService _failedFileService;
    private readonly IBackgroundTaskManager _backgroundTaskManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IFileStorage _fileStorage;
    private readonly IFrontendNotificationService _frontendNotificationService;
    private readonly ILogger<VideoProcessingService> _logger;
    private readonly ProcessingAvailability _processingAvailability;

    public VideoProcessingService(
        IUserPhotoSourceService userPhotoSourceService,
        IPhotoSourceService photoSourceService,
        IVideoService videoService,
        IFailedFileService failedFileService,
        IBackgroundTaskManager backgroundTaskManager,
        IServiceScopeFactory serviceScopeFactory,
        IFileStorage fileStorage,
        IFrontendNotificationService frontendNotificationService,
        ILogger<VideoProcessingService> logger,
        ProcessingAvailability processingAvailability)
    {
        _userPhotoSourceService = userPhotoSourceService;
        _photoSourceService = photoSourceService;
        _videoService = videoService;
        _failedFileService = failedFileService;
        _backgroundTaskManager = backgroundTaskManager;
        _serviceScopeFactory = serviceScopeFactory;
        _fileStorage = fileStorage;
        _frontendNotificationService = frontendNotificationService;
        _logger = logger;
        _processingAvailability = processingAvailability;
    }

    public async Task<bool> SupportsVideosAsync(long sourceId)
    {
        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);

        return IsYandexDisk(photoSource);
    }

    public async Task<bool> RunCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command)
    {
        if (command is PhotoSourceProcessingCommands.Start or PhotoSourceProcessingCommands.RetryFailed)
        {
            // the videos do not go through Kafka, but processing is available or not as a whole
            _processingAvailability.EnsureAvailable();

            return await StartAsync(userId, sourceId, retryFailed: command == PhotoSourceProcessingCommands.RetryFailed);
        }

        if (command == PhotoSourceProcessingCommands.Stop)
        {
            // the run records the Stopped status when it has finished
            _backgroundTaskManager.CancelTask(GetTaskName(userId, sourceId));
            return true;
        }

        throw new Exception("Unsupported command.");
    }

    public bool IsRunning(long userId, long sourceId)
    {
        return _backgroundTaskManager.IsRunning(GetTaskName(userId, sourceId));
    }

    public async Task<bool> DeleteDataAsync(long userId, long sourceId)
    {
        // a run saves videos while it goes, deleting them underneath it would only lose parts
        if (IsRunning(userId, sourceId))
        {
            return false;
        }

        await DeleteSourceDataAsync(userId, sourceId);

        return true;
    }

    public async Task<bool> DeleteAllDataAsync()
    {
        var userPhotoSources = await _userPhotoSourceService.GetAllUserPhotoSourceIdsAsync();

        // checked for all of them first, so that the data is deleted all or not at all
        if (userPhotoSources.Any(a => IsRunning(a.UserId, a.PhotoSourceId)))
        {
            return false;
        }

        foreach (var (userId, sourceId) in userPhotoSources)
        {
            await DeleteSourceDataAsync(userId, sourceId);
        }

        return true;
    }

    private async Task DeleteSourceDataAsync(long userId, long sourceId)
    {
        var previewPaths = await _videoService.DeleteByPhotoSourceAsync(userId, sourceId);

        foreach (var previewPath in previewPaths)
        {
            try
            {
                _fileStorage.Delete(previewPath);
            }
            catch (Exception e)
            {
                // the video is gone from the database either way, a preview left behind is not worth failing over
                _logger.LogWarning(e, "Failed to delete the video preview {PreviewPath}", previewPath);
            }
        }

        await _videoService.DeleteStatusAsync(userId, sourceId);
        await _failedFileService.DeleteVideosByPhotoSourceAsync(userId, sourceId);

        _logger.LogInformation("Deleted the videos of photo source {SourceId} of user {UserId}", sourceId, userId);

        // the pages watching the source are told it starts over
        await _frontendNotificationService.SendVideoProgressAsync(new UserPhotoSourceStatus
        {
            UserId = userId,
            PhotoSourceId = sourceId,
            Status = PhotoSourceStatus.NotStarted,
            LastUpdatedAt = DateTimeOffset.UtcNow
        });
    }

    /// <param name="retryFailed">Whether the run takes only the videos that failed in earlier runs. It is the same task
    /// as a run listing the disk, so that the two do not run at the same time.</param>
    private async Task<bool> StartAsync(long userId, long sourceId, bool retryFailed)
    {
        var taskName = GetTaskName(userId, sourceId);
        if (_backgroundTaskManager.IsRunning(taskName))
        {
            return false;
        }

        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);
        if (!IsYandexDisk(photoSource))
        {
            throw new InvalidOperationException($"The videos of photo source {photoSource.Name} cannot be processed.");
        }

        var authResult = await _userPhotoSourceService.GetAuthResultAsync(userId, sourceId);
        if (authResult == null || !authResult.IsValid)
        {
            throw new NotAuthorizedException("User is not authorized.");
        }

        var settings = new YandexDiskSettings();
        var run = new VideoRun(userId, sourceId, photoSource.Name, authResult.Token, Math.Max(settings.MaxParallelDownloads, 1),
            retryFailed);
        var serviceScopeFactory = _serviceScopeFactory;

        // a retry carries on the counters of the runs the videos failed in, a run lists the videos to count them
        var progress = retryFailed ? await CreateProgressAsync(userId, sourceId) : new ProcessingProgress(0, 0);

        return _backgroundTaskManager.TryStartTask(taskName,
            cancellationToken => DoWork(run, progress, serviceScopeFactory, cancellationToken));
    }

    /// <summary>
    /// Continues the counters of the earlier runs of the videos, which a retry of the failed ones changes.
    /// </summary>
    private async Task<ProcessingProgress> CreateProgressAsync(long userId, long sourceId)
    {
        var status = await _videoService.GetStatusAsync(userId, sourceId);

        return new ProcessingProgress(status?.ProcessedCount ?? 0, status?.FailedCount ?? 0)
        {
            TotalCount = status?.TotalCount ?? 0
        };
    }

    /// <summary>
    /// Runs in the background, with its own service scope: the scope of the request that started it is disposed
    /// by the time the run proceeds. Every video of the disk is listed, and the ones not saved yet have their
    /// preview downloaded and saved, several at a time, along with the metadata of the file, see
    /// <see cref="VideoExifReader"/>. A video that fails is not saved, so the next run tries it again, as it reads
    /// the metadata of the videos saved without it. The videos that fail are recorded, for a retry to take on its
    /// own, looked up by their paths again, and the ones saved are no longer failed.
    /// </summary>
    private static async Task DoWork(VideoRun run, ProcessingProgress progress, IServiceScopeFactory serviceScopeFactory,
        CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var videoService = scope.ServiceProvider.GetRequiredService<IVideoService>();
        var failedFileService = scope.ServiceProvider.GetRequiredService<IFailedFileService>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var frontendNotificationService = scope.ServiceProvider.GetRequiredService<IFrontendNotificationService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<VideoProcessingService>>();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var applicationLifetime = scope.ServiceProvider.GetService<IHostApplicationLifetime>();

        var status = PhotoSourceStatus.Done;
        using var reportingCancellationTokenSource = new CancellationTokenSource();
        var reportingTask = Task.CompletedTask;

        try
        {
            await ReportStatusAsync(serviceScopeFactory, logger, run, PhotoSourceStatus.InProgress, progress);

            // Yandex.Disk tokens are not refreshed, the user authorizes again when the token has expired
            var apiClient = new ApiClient(run.Token, httpClientFactory.CreateClient("yandexDiskClient"));
            var videoSource = new YandexDiskVideoSource(apiClient, logger);

            List<VideoToSave> videosToSave;

            if (run.RetryFailed)
            {
                var failedFiles = await failedFileService.GetVideosAsync(run.UserId, run.SourceId);

                // the videos retried are counted as failed already, the counters of the earlier runs stay
                progress.RetryTotalCount = failedFiles.Count;
                videosToSave = failedFiles.Select(a => new VideoToSave(a.ExternalId, a.Path, a.FileName, null)).ToList();

                reportingTask = ReportStatusPeriodicallyAsync(serviceScopeFactory, logger, run, progress,
                    reportingCancellationTokenSource.Token);

                logger.LogInformation("{VideoCount} failed videos of {PhotoSourceName} to retry for user {UserId}",
                    videosToSave.Count, run.PhotoSourceName, run.UserId);
            }
            else
            {
                var videos = await videoSource.ListVideosAsync(cancellationToken);
                var savedExternalIds = await videoService.GetSavedExternalIdsAsync(run.UserId, run.SourceId,
                    videos.Select(a => a.ResourceId));

                // the videos saved before their metadata was read, or whose metadata could not be downloaded, have it
                // read now
                var idsWithoutExif = await videoService.GetIdsWithoutExifAsync(run.UserId, run.SourceId);
                var videosToReadExif = videos
                    .Where(a => savedExternalIds.Contains(a.ResourceId) && idsWithoutExif.ContainsKey(a.ResourceId))
                    .Select(a => (Id: idsWithoutExif[a.ResourceId], Resource: a))
                    .ToList();

                // the videos saved by earlier runs count as processed, a run resumed carries on from them
                progress = new ProcessingProgress(savedExternalIds.Count - videosToReadExif.Count, 0) { TotalCount = videos.Count };

                reportingTask = ReportStatusPeriodicallyAsync(serviceScopeFactory, logger, run, progress,
                    reportingCancellationTokenSource.Token);

                videosToSave = videos
                    .Where(a => !savedExternalIds.Contains(a.ResourceId))
                    .Select(a => new VideoToSave(a.ResourceId, a.Path, a.Name, a))
                    .ToList();

                logger.LogInformation(
                    "{VideoCount} of {TotalCount} videos of {PhotoSourceName} to save, and {ExifCount} to read the metadata of, for user {UserId}",
                    videosToSave.Count, videos.Count, run.PhotoSourceName, videosToReadExif.Count, run.UserId);

                await ReadMissingExifAsync(videoSource, videoService, logger, run, videosToReadExif, progress, cancellationToken);
            }

            foreach (var chunk in videosToSave.Chunk(SaveEvery))
            {
                var attempts = new ConcurrentBag<VideoAttempt>();
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = run.MaxParallelDownloads,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(chunk, parallelOptions, async (video, token) =>
                    attempts.Add(await SavePreviewOrSkipAsync(videoSource, fileStorage, logger, run, video, token)));

                await videoService.AddRangeAsync(attempts.Where(a => a.Video != null).Select(a => a.Video!).ToList());

                await RecordChunkAsync(failedFileService, progress, run, attempts);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // a run is cancelled by the user stopping it or by the application stopping, which cancels every run
            status = applicationLifetime?.ApplicationStopping.IsCancellationRequested == true
                ? PhotoSourceStatus.Paused
                : PhotoSourceStatus.Stopped;
        }
        catch (Exception e)
        {
            status = PhotoSourceStatus.Failed;

            logger.LogError(e, "Processing of the videos of {PhotoSourceName} for user {UserId} failed", run.PhotoSourceName,
                run.UserId);

            await frontendNotificationService.SendVideoErrorAsync(run.UserId, run.SourceId, e.Message);
        }
        finally
        {
            await reportingCancellationTokenSource.CancelAsync();
            await reportingTask;

            await ReportStatusAsync(serviceScopeFactory, logger, run, status, progress);
        }
    }

    /// <summary>
    /// Counts the videos of a chunk, all of them saved or failed, and records the ones that failed, for a retry to
    /// take, while the ones saved are no longer failed.
    /// </summary>
    private static Task RecordChunkAsync(IFailedFileService failedFileService, ProcessingProgress progress, VideoRun run,
        IEnumerable<VideoAttempt> attempts)
    {
        var failedFiles = new List<FailedFile>();
        var savedExternalIds = new List<string>();

        foreach (var attempt in attempts)
        {
            if (attempt.Video != null)
            {
                // a video retried had been counted as failed, it is only counted again when it is saved this time
                if (run.RetryFailed)
                {
                    progress.FileRecovered();
                }
                else
                {
                    progress.FileProcessed();
                }

                savedExternalIds.Add(attempt.Item.ExternalId);
            }
            else
            {
                if (run.RetryFailed)
                {
                    progress.FileFailedAgain();
                }
                else
                {
                    progress.FileFailed();
                }

                failedFiles.Add(new FailedFile
                {
                    ExternalId = attempt.Item.ExternalId,
                    Path = attempt.Item.Path,
                    FileName = attempt.Item.FileName,
                    Stage = attempt.Stage,
                    Error = attempt.Error ?? "Saving the preview failed.",
                    FailedAt = DateTimeOffset.UtcNow,
                    FromVideos = true
                });
            }
        }

        return failedFileService.RecordAsync(run.UserId, run.SourceId, failedFiles, savedExternalIds);
    }

    /// <summary>
    /// Downloads the preview of the video, looked up by its path first when it is retried, and stores it as it is.
    /// A video whose preview cannot be had is skipped, unless Yandex.Disk no longer accepts the token, which fails
    /// every other video as well.
    /// </summary>
    /// <returns>The video to save, or why it is skipped.</returns>
    private static async Task<VideoAttempt> SavePreviewOrSkipAsync(YandexDiskVideoSource videoSource, IFileStorage fileStorage,
        ILogger logger, VideoRun run, VideoToSave item, CancellationToken cancellationToken)
    {
        try
        {
            var resource = item.Resource ?? await GetFailedVideoAsync(videoSource, item, cancellationToken);

            var previewUrl = YandexDiskVideoSource.GetPreviewUrl(resource);
            if (previewUrl == null)
            {
                logger.LogWarning("{FileName} has no preview of any of the sizes {PreviewSizes}, skipping it", resource.Name,
                    string.Join(", ", YandexDiskVideoSource.PreviewSizeNames));

                return VideoAttempt.Failed(item, FileFailureStage.Download, "Yandex.Disk has made no preview of the video.");
            }

            var (contents, contentType) = await videoSource.DownloadPreviewAsync(previewUrl, cancellationToken);
            if (contents.Length == 0)
            {
                logger.LogWarning("The preview of {FileName} has been downloaded empty, skipping it", resource.Name);

                return VideoAttempt.Failed(item, FileFailureStage.Download, "The preview has been downloaded empty.");
            }

            var previewFilePath = GetPreviewFilePath(run, resource, contentType);

            try
            {
                await fileStorage.SaveAsync(previewFilePath, contents);
            }
            catch (IOException e)
            {
                logger.LogError(e, "Failed to store the preview of {FileName}", resource.Name);

                return VideoAttempt.Failed(item, FileFailureStage.Processing, "Failed to store the preview: " + e.Message);
            }

            var video = new Video
            {
                UserId = run.UserId,
                PhotoSourceId = run.SourceId,
                ExternalId = resource.ResourceId,
                FileName = resource.Name,
                FolderPath = GetFolderPath(resource.Path),
                MimeType = resource.MimeType,
                Size = resource.Size,
                // the time the video was taken, when Yandex.Disk has read it, otherwise the upload time
                DateTimeTaken = ToUtc(resource.Exif?.DateTime ?? resource.PhotosliceTime ?? resource.Created),
                ExifDateTime = resource.Exif?.DateTime is { } exifDateTime ? ToUtc(exifDateTime) : null,
                Latitude = resource.Exif?.GpsLatitude,
                Longitude = resource.Exif?.GpsLongitude,
                PreviewFilePath = previewFilePath,
                PreviewContentType = contentType ?? "image/jpeg",
                AddedOn = DateTimeOffset.UtcNow
            };

            // the video is saved without its metadata when it cannot be downloaded, the next run reads it
            var exif = await TryReadExifAsync(videoSource, logger, resource, cancellationToken);
            exif?.ApplyTo(video);

            return new VideoAttempt(item, video, FileFailureStage.Download, null);
        }
        catch (PhotoSourceException e) when (!e.IsAuthError)
        {
            // the error has been logged
            return VideoAttempt.Failed(item, FileFailureStage.Download, e.Message);
        }
    }

    /// <exception cref="PhotoSourceException">The video could not be looked up.</exception>
    private static Task<Resource> GetFailedVideoAsync(YandexDiskVideoSource videoSource, VideoToSave video,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(video.Path))
        {
            throw new YandexDiskException("Yandex.Disk files are downloaded by path, the path is missing.");
        }

        return videoSource.GetVideoAsync(video.Path, cancellationToken);
    }

    /// <summary>
    /// Reads the metadata of the videos saved without it, several at a time, and saves it a chunk at a time. A
    /// video whose metadata cannot be downloaded counts as failed, and is left for the next run.
    /// </summary>
    private static async Task ReadMissingExifAsync(YandexDiskVideoSource videoSource, IVideoService videoService, ILogger logger,
        VideoRun run, IReadOnlyList<(long Id, Resource Resource)> videos, ProcessingProgress progress,
        CancellationToken cancellationToken)
    {
        foreach (var chunk in videos.Chunk(SaveEvery))
        {
            var exifByVideoId = new ConcurrentDictionary<long, VideoExif>();
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = run.MaxParallelDownloads,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(chunk, parallelOptions, async (video, token) =>
            {
                var exif = await TryReadExifAsync(videoSource, logger, video.Resource, token);
                if (exif != null)
                {
                    exifByVideoId[video.Id] = exif;
                }
                else
                {
                    progress.FileFailed();
                }
            });

            await videoService.UpdateExifAsync(exifByVideoId);

            foreach (var _ in exifByVideoId)
            {
                progress.FileProcessed();
            }
        }
    }

    /// <summary>
    /// Reads the metadata of the video file. A file whose metadata cannot be made out has none, so that it is not
    /// read again, while one that cannot be downloaded is read on the next run.
    /// </summary>
    /// <returns>The metadata, null when the file cannot be downloaded.</returns>
    /// <exception cref="PhotoSourceException">Yandex.Disk no longer accepts the token.</exception>
    private static async Task<VideoExif?> TryReadExifAsync(YandexDiskVideoSource videoSource, ILogger logger,
        Resource resource, CancellationToken cancellationToken)
    {
        try
        {
            return await videoSource.ReadExifAsync(resource, cancellationToken);
        }
        catch (PhotoSourceException e) when (!e.IsAuthError)
        {
            logger.LogWarning(e, "The metadata of {FileName} cannot be downloaded, it is left for the next run", resource.Name);

            return null;
        }
        catch (Exception e) when (e is not PhotoSourceException and not OperationCanceledException)
        {
            // MetadataExtractor fails the same way on the same file
            logger.LogWarning(e, "The metadata of {FileName} cannot be read, it is saved without it", resource.Name);

            return VideoExif.Empty;
        }
    }

    /// <summary>
    /// The folder of a file, from its path: <c>disk:/Camera Uploads</c> for <c>disk:/Camera Uploads/video.mp4</c>,
    /// and <c>disk:/</c> for a file at the root of the disk.
    /// </summary>
    public static string? GetFolderPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0)
        {
            return null;
        }

        var folderPath = path[..lastSlash];

        return folderPath.EndsWith(':') ? folderPath + "/" : folderPath;
    }

    /// <summary>
    /// Dates are stored in timestamptz columns, which only accept UTC. The dates Yandex.Disk sends carry an offset,
    /// which makes them local times once deserialized; one without an offset is taken as UTC.
    /// </summary>
    private static DateTimeOffset ToUtc(DateTime value)
    {
        var dateTime = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

        return new DateTimeOffset(dateTime);
    }

    /// <summary>
    /// Videos of the same name get previews of their own: the name carries a hash of the ID of the video, which
    /// stays the same for it, so processing it again overwrites its preview.
    /// </summary>
    private static string GetPreviewFilePath(VideoRun run, Resource resource, string? contentType)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(resource.Name);
        var fileKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(resource.ResourceId)))[..16];
        var extension = contentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };

        return Path.Combine(run.PhotoSourceName, run.UserId.ToString(), "video-previews",
            $"{fileNameWithoutExtension}_{fileKey}{extension}");
    }

    private static async Task ReportStatusPeriodicallyAsync(IServiceScopeFactory serviceScopeFactory, ILogger logger, VideoRun run,
        ProcessingProgress progress, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(StatusReportInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await ReportStatusAsync(serviceScopeFactory, logger, run, PhotoSourceStatus.InProgress, progress);
            }
        }
        catch (OperationCanceledException)
        {
            // processing has finished
        }
    }

    /// <summary>
    /// Saves the status and counters to the database and sends them to the frontend. Failures are only logged,
    /// they must not stop the processing.
    /// </summary>
    private static async Task ReportStatusAsync(IServiceScopeFactory serviceScopeFactory, ILogger logger, VideoRun run,
        PhotoSourceStatus status, ProcessingProgress progress)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var videoService = scope.ServiceProvider.GetRequiredService<IVideoService>();
            var frontendNotificationService = scope.ServiceProvider.GetRequiredService<IFrontendNotificationService>();

            var videoSourceStatus = new UserPhotoSourceStatus
            {
                UserId = run.UserId,
                PhotoSourceId = run.SourceId,
                Status = status,
                TotalCount = progress.TotalCount,
                ProcessedCount = progress.ProcessedCount,
                FailedCount = progress.FailedCount,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };

            await videoService.UpdateStatusAsync(videoSourceStatus);

            if (run.RetryFailed)
            {
                await frontendNotificationService.SendVideoProgressAsync(videoSourceStatus,
                    new RetryProgress(progress.RetryTotalCount, progress.RetriedCount, progress.RecoveredCount));
            }
            else
            {
                await frontendNotificationService.SendVideoProgressAsync(videoSourceStatus);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to report the status of the videos of photo source {SourceId} for user {UserId}",
                run.SourceId, run.UserId);
        }
    }

    private static bool IsYandexDisk(PhotoSource photoSource)
    {
        return Type.GetType(photoSource.ServiceFactoryImplementationType) == typeof(YandexDiskDownloadServiceFactory);
    }

    public int StopAllRuns()
    {
        return _backgroundTaskManager.CancelTasks(a => a.StartsWith(TaskNamePrefix, StringComparison.Ordinal)).Count;
    }

    private const string TaskNamePrefix = "Videos-";

    private static string GetTaskName(long userId, long sourceId)
    {
        return $"{TaskNamePrefix}UserId={userId}-SourceId={sourceId}";
    }

    /// <param name="RetryFailed">The run takes only the videos that failed in earlier runs, instead of listing the disk.</param>
    private sealed record VideoRun(long UserId, long SourceId, string PhotoSourceName, string Token, int MaxParallelDownloads,
        bool RetryFailed);

    /// <summary>
    /// A video to save: one the listing has given, or one that failed before, which is looked up by its path again.
    /// </summary>
    /// <param name="Resource">The video as the listing has given it, null for a video retried.</param>
    private sealed record VideoToSave(string ExternalId, string? Path, string FileName, Resource? Resource);

    /// <summary>
    /// The outcome of saving the preview of a video: the video to save, or what it failed at and why.
    /// </summary>
    private sealed record VideoAttempt(VideoToSave Item, Video? Video, FileFailureStage Stage, string? Error)
    {
        public static VideoAttempt Failed(VideoToSave item, FileFailureStage stage, string error) => new(item, null, stage, error);
    }
}
