using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Queues;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services;

public class OtherFoldersProcessingService : IOtherFoldersProcessingService
{
    private static readonly TimeSpan StatusReportInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How many photos have their previews downloaded and processed before the next ones start, which is how much of
    /// a stopped run is downloaded again when it resumes.
    /// </summary>
    private const int SaveEvery = 100;

    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly IPhotoSourceService _photoSourceService;
    private readonly IBackgroundTaskManager _backgroundTaskManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly PhotoProcessingSettings _photoProcessingSettings;

    public OtherFoldersProcessingService(
        IUserPhotoSourceService userPhotoSourceService,
        IPhotoSourceService photoSourceService,
        IBackgroundTaskManager backgroundTaskManager,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<PhotoProcessingSettings> photoProcessingSettings)
    {
        _userPhotoSourceService = userPhotoSourceService;
        _photoSourceService = photoSourceService;
        _backgroundTaskManager = backgroundTaskManager;
        _serviceScopeFactory = serviceScopeFactory;
        _photoProcessingSettings = photoProcessingSettings.Value;
    }

    public async Task<bool> SupportsOtherFoldersAsync(long sourceId)
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

        // a photo that failed is not saved, so the next run tries it again with the rest
        throw new Exception("Unsupported command.");
    }

    public bool IsRunning(long userId, long sourceId)
    {
        return _backgroundTaskManager.IsRunning(GetTaskName(userId, sourceId));
    }

    /// <summary>
    /// What the photo is saved with: where and when it was taken as Yandex.Disk has read them from the photo, as the
    /// preview may have no EXIF, and the hash of the photo itself, so that it is found a copy of the same photo saved
    /// from its original.
    /// </summary>
    public static DownloadedFileInfo CreateFileInfo(Resource resource)
    {
        // the time the photo was taken, when Yandex.Disk has read it from EXIF, otherwise the upload time
        var createdOn = resource.Exif?.DateTime ?? resource.PhotosliceTime ?? resource.Created;

        return new DownloadedFileInfo(resource.Name, resource.Path, createdOn, resource.ResourceId)
        {
            Latitude = resource.Exif?.GpsLatitude,
            Longitude = resource.Exif?.GpsLongitude,
            ContentHash = string.IsNullOrEmpty(resource.Sha256) ? null : resource.Sha256.ToLowerInvariant(),
            FromOtherFolder = true
        };
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
            throw new InvalidOperationException($"The other folders of photo source {photoSource.Name} cannot be processed.");
        }

        var authResult = await _userPhotoSourceService.GetAuthResultAsync(userId, sourceId);
        if (authResult == null || !authResult.IsValid)
        {
            throw new NotAuthorizedException("User is not authorized.");
        }

        var settings = JsonSerializer.Deserialize<YandexDiskSettings>(photoSource.ServiceSettings) ?? new YandexDiskSettings();
        var run = new OtherFoldersRun(userId, sourceId, photoSource.Name, authResult.Token, settings, _photoProcessingSettings.Sizes);
        var serviceScopeFactory = _serviceScopeFactory;

        return _backgroundTaskManager.TryStartTask(taskName, cancellationToken => DoWork(run, serviceScopeFactory, cancellationToken));
    }

    /// <summary>
    /// Runs in the background, with its own service scope: the scope of the request that started it is disposed
    /// by the time the run proceeds. Every photo of the disk outside the folder it is processed from is listed, and
    /// the ones not saved yet have their preview downloaded, several at a time, and processed as a downloaded photo
    /// is. A photo that fails is not saved, so the next run tries it again.
    /// </summary>
    private static async Task DoWork(OtherFoldersRun run, IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var photoService = scope.ServiceProvider.GetRequiredService<IPhotoService>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var requestQueue = scope.ServiceProvider.GetRequiredService<IMessageQueue<ProcessImageRequest>>();
        var frontendNotificationService = scope.ServiceProvider.GetRequiredService<IFrontendNotificationService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OtherFoldersProcessingService>>();
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
            var otherFolderPhotos = new YandexDiskOtherFolderPhotos(apiClient, logger);

            var sourceFolder = await YandexDiskDownloadService.GetSourceFolderAsync(apiClient, run.Settings, logger, cancellationToken);
            var photos = await otherFolderPhotos.ListPhotosAsync(sourceFolder, cancellationToken);
            var savedExternalIds = await photoService.GetSavedExternalIdsAsync(run.UserId, run.SourceId,
                photos.Select(a => a.ResourceId));

            // the photos saved by earlier runs count as processed, a run resumed carries on from them
            progress = new ProcessingProgress(savedExternalIds.Count, 0) { TotalCount = photos.Count };

            reportingTask = ReportStatusPeriodicallyAsync(serviceScopeFactory, logger, run, progress,
                reportingCancellationTokenSource.Token);

            var photosToSave = photos.Where(a => !savedExternalIds.Contains(a.ResourceId)).ToList();

            logger.LogInformation("{PhotoCount} of {TotalCount} photos outside {SourceFolder} of {PhotoSourceName} to save for user {UserId}",
                photosToSave.Count, photos.Count, sourceFolder, run.PhotoSourceName, run.UserId);

            foreach (var chunk in photosToSave.Chunk(SaveEvery))
            {
                var processedTasks = new ConcurrentBag<Task<ProcessingResult>>();
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(run.Settings.MaxParallelDownloads, 1),
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(chunk, parallelOptions, async (resource, token) =>
                {
                    var processed = await EnqueuePreviewOrSkipAsync(otherFolderPhotos, fileStorage, requestQueue, logger, run,
                        resource, token);
                    if (processed != null)
                    {
                        processedTasks.Add(processed);
                    }
                    else
                    {
                        progress.FileFailed();
                    }
                });

                // the photos of the chunk are saved before the next ones start, so that a stopped run loses no more
                var results = await Task.WhenAll(processedTasks).WaitAsync(cancellationToken);

                foreach (var result in results)
                {
                    if (result.Succeeded)
                    {
                        progress.FileProcessed();
                    }
                    else
                    {
                        progress.FileFailed();
                    }
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

            logger.LogError(e, "Processing of the other folders of {PhotoSourceName} for user {UserId} failed", run.PhotoSourceName,
                run.UserId);

            await frontendNotificationService.SendOtherFoldersErrorAsync(run.UserId, run.SourceId, e.Message);
        }
        finally
        {
            await reportingCancellationTokenSource.CancelAsync();
            await reportingTask;

            await ReportStatusAsync(serviceScopeFactory, logger, run, status, progress);
        }
    }

    /// <summary>
    /// Downloads the preview of the photo and hands it over to be processed. A photo whose preview cannot be had is
    /// skipped, unless Yandex.Disk no longer accepts the token, which fails every other photo as well.
    /// </summary>
    /// <returns>What the processing of the photo ends with, null when it is skipped.</returns>
    private static async Task<Task<ProcessingResult>?> EnqueuePreviewOrSkipAsync(YandexDiskOtherFolderPhotos otherFolderPhotos,
        IFileStorage fileStorage, IMessageQueue<ProcessImageRequest> requestQueue, ILogger logger, OtherFoldersRun run,
        Resource resource, CancellationToken cancellationToken)
    {
        var previewUrl = YandexDiskOtherFolderPhotos.GetPreviewUrl(resource);
        if (previewUrl == null)
        {
            logger.LogWarning("{FileName} has no preview, skipping it", resource.Path);

            return null;
        }

        try
        {
            var contents = await otherFolderPhotos.DownloadPreviewAsync(previewUrl, cancellationToken);
            if (contents.Length == 0)
            {
                logger.LogWarning("The preview of {FileName} has been downloaded empty, skipping it", resource.Path);

                return null;
            }

            // a path of its own per download: photos of the same name, from other folders, would overwrite each
            // other while waiting to be processed
            var fileName = await fileStorage.SaveAsync($"Bin/{run.UserId}/{run.SourceId}/{Guid.NewGuid():N}-{resource.Name}",
                contents);

            var processed = new TaskCompletionSource<ProcessingResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            await requestQueue.EnqueueAsync(new ProcessImageRequest
            {
                DownloadedFileInfo = CreateFileInfo(resource),
                FileName = fileName,
                Sizes = run.Sizes,
                UserId = run.UserId,
                PhotoSourceId = run.SourceId,
                PhotoSourceName = run.PhotoSourceName,
                Processed = processed
            }, cancellationToken);

            return processed.Task;
        }
        catch (PhotoSourceException e) when (!e.IsAuthError)
        {
            // the error has been logged
            return null;
        }
        catch (IOException e)
        {
            logger.LogError(e, "Failed to store the preview of {FileName}", resource.Path);

            return null;
        }
    }

    private static async Task ReportStatusPeriodicallyAsync(IServiceScopeFactory serviceScopeFactory, ILogger logger,
        OtherFoldersRun run, ProcessingProgress progress, CancellationToken cancellationToken)
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
    private static async Task ReportStatusAsync(IServiceScopeFactory serviceScopeFactory, ILogger logger, OtherFoldersRun run,
        PhotoSourceStatus status, ProcessingProgress progress)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var userPhotoSourceService = scope.ServiceProvider.GetRequiredService<IUserPhotoSourceService>();
            var frontendNotificationService = scope.ServiceProvider.GetRequiredService<IFrontendNotificationService>();

            var otherFoldersStatus = new UserPhotoSourceStatus
            {
                UserId = run.UserId,
                PhotoSourceId = run.SourceId,
                Status = status,
                TotalCount = progress.TotalCount,
                ProcessedCount = progress.ProcessedCount,
                FailedCount = progress.FailedCount,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };

            await userPhotoSourceService.UpdateOtherFoldersStatusAsync(otherFoldersStatus);
            await frontendNotificationService.SendOtherFoldersProgressAsync(otherFoldersStatus);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to report the status of the other folders of photo source {SourceId} for user {UserId}",
                run.SourceId, run.UserId);
        }
    }

    private static bool IsYandexDisk(PhotoSource photoSource)
    {
        return Type.GetType(photoSource.ServiceFactoryImplementationType) == typeof(YandexDiskDownloadServiceFactory);
    }

    private static string GetTaskName(long userId, long sourceId)
    {
        return $"OtherFolders-UserId={userId}-SourceId={sourceId}";
    }

    private sealed record OtherFoldersRun(long UserId, long SourceId, string PhotoSourceName, string Token,
        YandexDiskSettings Settings, int[] Sizes);
}
