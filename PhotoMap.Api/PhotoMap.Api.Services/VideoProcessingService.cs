using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private readonly IBackgroundTaskManager _backgroundTaskManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IFileStorage _fileStorage;
    private readonly IFrontendNotificationService _frontendNotificationService;
    private readonly ILogger<VideoProcessingService> _logger;

    public VideoProcessingService(
        IUserPhotoSourceService userPhotoSourceService,
        IPhotoSourceService photoSourceService,
        IVideoService videoService,
        IBackgroundTaskManager backgroundTaskManager,
        IServiceScopeFactory serviceScopeFactory,
        IFileStorage fileStorage,
        IFrontendNotificationService frontendNotificationService,
        ILogger<VideoProcessingService> logger)
    {
        _userPhotoSourceService = userPhotoSourceService;
        _photoSourceService = photoSourceService;
        _videoService = videoService;
        _backgroundTaskManager = backgroundTaskManager;
        _serviceScopeFactory = serviceScopeFactory;
        _fileStorage = fileStorage;
        _frontendNotificationService = frontendNotificationService;
        _logger = logger;
    }

    public async Task<bool> SupportsVideosAsync(long sourceId)
    {
        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);

        return IsYandexDisk(photoSource);
    }

    public async Task<bool> RunCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command)
    {
        if (command == PhotoSourceProcessingCommands.Start)
        {
            return await StartAsync(userId, sourceId);
        }

        if (command == PhotoSourceProcessingCommands.Stop)
        {
            // the run records the Stopped status when it has finished
            _backgroundTaskManager.CancelTask(GetTaskName(userId, sourceId));
            return true;
        }

        // a video that failed is not saved, so the next run tries it again with the rest
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

    private async Task<bool> StartAsync(long userId, long sourceId)
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

        var settings = JsonSerializer.Deserialize<YandexDiskSettings>(photoSource.ServiceSettings) ?? new YandexDiskSettings();
        var run = new VideoRun(userId, sourceId, photoSource.Name, authResult.Token, Math.Max(settings.MaxParallelDownloads, 1));
        var serviceScopeFactory = _serviceScopeFactory;

        return _backgroundTaskManager.TryStartTask(taskName, cancellationToken => DoWork(run, serviceScopeFactory, cancellationToken));
    }

    /// <summary>
    /// Runs in the background, with its own service scope: the scope of the request that started it is disposed
    /// by the time the run proceeds. Every video of the disk is listed, and the ones not saved yet have their
    /// preview downloaded and saved, several at a time. A video that fails is not saved, so the next run tries it
    /// again.
    /// </summary>
    private static async Task DoWork(VideoRun run, IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var videoService = scope.ServiceProvider.GetRequiredService<IVideoService>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var frontendNotificationService = scope.ServiceProvider.GetRequiredService<IFrontendNotificationService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<VideoProcessingService>>();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var applicationLifetime = scope.ServiceProvider.GetService<IHostApplicationLifetime>();

        var progress = new ProcessingProgress(0, 0);
        var status = PhotoSourceStatus.Done;
        using var reportingCancellationTokenSource = new CancellationTokenSource();
        var reportingTask = Task.CompletedTask;

        try
        {
            await ReportStatusAsync(serviceScopeFactory, logger, run, PhotoSourceStatus.InProgress, progress);

            // Yandex.Disk tokens are not refreshed, the user authorizes again when the token has expired
            var apiClient = new ApiClient(run.Token, httpClientFactory.CreateClient("yandexDiskClient"));
            var videoSource = new YandexDiskVideoSource(apiClient, logger);

            var videos = await videoSource.ListVideosAsync(cancellationToken);
            var savedExternalIds = await videoService.GetSavedExternalIdsAsync(run.UserId, run.SourceId,
                videos.Select(a => a.ResourceId));

            // the videos saved by earlier runs count as processed, a run resumed carries on from them
            progress = new ProcessingProgress(savedExternalIds.Count, 0) { TotalCount = videos.Count };

            reportingTask = ReportStatusPeriodicallyAsync(serviceScopeFactory, logger, run, progress,
                reportingCancellationTokenSource.Token);

            var videosToSave = videos.Where(a => !savedExternalIds.Contains(a.ResourceId)).ToList();

            logger.LogInformation("{VideoCount} of {TotalCount} videos of {PhotoSourceName} to save for user {UserId}",
                videosToSave.Count, videos.Count, run.PhotoSourceName, run.UserId);

            foreach (var chunk in videosToSave.Chunk(SaveEvery))
            {
                var savedVideos = new ConcurrentBag<Video>();
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = run.MaxParallelDownloads,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(chunk, parallelOptions, async (resource, token) =>
                {
                    var video = await SavePreviewOrSkipAsync(videoSource, fileStorage, logger, run, resource, token);
                    if (video != null)
                    {
                        savedVideos.Add(video);
                    }
                    else
                    {
                        progress.FileFailed();
                    }
                });

                await videoService.AddRangeAsync(savedVideos.ToList());

                foreach (var _ in savedVideos)
                {
                    progress.FileProcessed();
                }
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
    /// Downloads the preview of the video and stores it as it is. A video whose preview cannot be had is skipped,
    /// unless Yandex.Disk no longer accepts the token, which fails every other video as well.
    /// </summary>
    /// <returns>The video to save, null when it is skipped.</returns>
    private static async Task<Video?> SavePreviewOrSkipAsync(YandexDiskVideoSource videoSource, IFileStorage fileStorage,
        ILogger logger, VideoRun run, Resource resource, CancellationToken cancellationToken)
    {
        var previewUrl = YandexDiskVideoSource.GetPreviewUrl(resource);
        if (previewUrl == null)
        {
            logger.LogWarning("{FileName} has no preview of any of the sizes {PreviewSizes}, skipping it", resource.Name,
                string.Join(", ", YandexDiskVideoSource.PreviewSizeNames));

            return null;
        }

        try
        {
            var (contents, contentType) = await videoSource.DownloadPreviewAsync(previewUrl, cancellationToken);
            if (contents.Length == 0)
            {
                logger.LogWarning("The preview of {FileName} has been downloaded empty, skipping it", resource.Name);

                return null;
            }

            var previewFilePath = GetPreviewFilePath(run, resource, contentType);

            await fileStorage.SaveAsync(previewFilePath, contents);

            return new Video
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
        }
        catch (PhotoSourceException e) when (!e.IsAuthError)
        {
            // the error has been logged
            return null;
        }
        catch (IOException e)
        {
            logger.LogError(e, "Failed to store the preview of {FileName}", resource.Name);

            return null;
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
            await frontendNotificationService.SendVideoProgressAsync(videoSourceStatus);
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

    private static string GetTaskName(long userId, long sourceId)
    {
        return $"Videos-UserId={userId}-SourceId={sourceId}";
    }

    private sealed record VideoRun(long UserId, long SourceId, string PhotoSourceName, string Token, int MaxParallelDownloads);
}
