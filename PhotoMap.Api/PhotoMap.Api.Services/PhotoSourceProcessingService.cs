using System.Collections.Concurrent;
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

public class PhotoSourceProcessingService : IPhotoSourceProcessingService
{
    private static readonly TimeSpan StatusReportInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How many photos of the other folders are downloaded and processed before the next ones start, which is how
    /// much of a stopped run is downloaded again when it resumes.
    /// </summary>
    private const int OtherFoldersSaveEvery = 100;

    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly IPhotoSourceService _photoSourceService;
    private readonly IBackgroundTaskManager _backgroundTaskManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly PhotoProcessingSettings _photoProcessingSettings;

    public PhotoSourceProcessingService(
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

    public async Task<bool> RunCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command)
    {
        return command switch
        {
            PhotoSourceProcessingCommands.Start => await StartAsync(userId, sourceId, RunKind.Source),
            PhotoSourceProcessingCommands.RetryFailed => await StartAsync(userId, sourceId, RunKind.RetryFailed),
            PhotoSourceProcessingCommands.Stop => Stop(userId, sourceId, RunKind.Source),
            _ => throw new Exception("Unsupported command.")
        };
    }

    public bool IsRunning(long userId, long sourceId)
    {
        return _backgroundTaskManager.IsRunning(GetTaskName(userId, sourceId, RunKind.Source));
    }

    public async Task<bool> SupportsOtherFoldersAsync(long sourceId)
    {
        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);

        return IsYandexDisk(photoSource);
    }

    public async Task<bool> RunOtherFoldersCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command)
    {
        // a photo of the other folders that failed is not saved, so the next run tries it again with the rest
        return command switch
        {
            PhotoSourceProcessingCommands.Start => await StartAsync(userId, sourceId, RunKind.OtherFolders),
            PhotoSourceProcessingCommands.Stop => Stop(userId, sourceId, RunKind.OtherFolders),
            _ => throw new Exception("Unsupported command.")
        };
    }

    public bool IsOtherFoldersRunning(long userId, long sourceId)
    {
        return _backgroundTaskManager.IsRunning(GetTaskName(userId, sourceId, RunKind.OtherFolders));
    }

    /// <summary>
    /// What a photo of the other folders is saved with, as a photo of the folder the source is processed from is:
    /// where and when it was taken, and its hash, are read from the photo downloaded.
    /// </summary>
    public static DownloadedFileInfo CreateFileInfo(Resource resource)
    {
        // the time the photo was taken, when Yandex.Disk has read it from EXIF, otherwise the upload time
        var createdOn = resource.Exif?.DateTime ?? resource.PhotosliceTime ?? resource.Created;

        return new DownloadedFileInfo(resource.Name, resource.Path, createdOn, resource.ResourceId)
        {
            FromOtherFolder = true
        };
    }

    private bool Stop(long userId, long sourceId, RunKind kind)
    {
        // the run records the Stopped status when it has finished
        _backgroundTaskManager.CancelTask(GetTaskName(userId, sourceId, kind));
        return true;
    }

    /// <param name="kind">A retry of the failed files is the same task as a regular run of the source, so that the
    /// two do not run at the same time. The other folders run apart from them.</param>
    private async Task<bool> StartAsync(long userId, long sourceId, RunKind kind)
    {
        var taskName = GetTaskName(userId, sourceId, kind);
        if (_backgroundTaskManager.IsRunning(taskName))
        {
            return false;
        }

        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);
        if (kind == RunKind.OtherFolders && !IsYandexDisk(photoSource))
        {
            throw new InvalidOperationException($"The other folders of photo source {photoSource.Name} cannot be processed.");
        }

        var authResult = await GetAuthResultAsync(userId, sourceId);
        var run = new ProcessingRun(userId, sourceId, photoSource.Name, _photoProcessingSettings.Sizes, kind);
        var serviceScopeFactory = _serviceScopeFactory;

        Func<CancellationToken, Task> work;
        if (kind == RunKind.OtherFolders)
        {
            var settings = new YandexDiskSettings();

            work = cancellationToken => DoOtherFoldersWork(run, authResult.Token, settings, serviceScopeFactory, cancellationToken);
        }
        else
        {
            var parameters = new DownloadServiceParameters
            {
                UserId = userId,
                SourceId = sourceId,
                AuthResult = authResult,
                ClientId = photoSource.ClientAuthSettings.OAuthConfiguration.ClientId,
                Progress = await CreateProgressAsync(userId, sourceId)
            };

            work = cancellationToken => DoWork(run, photoSource, parameters, serviceScopeFactory, cancellationToken);
        }

        return _backgroundTaskManager.TryStartTask(taskName, work);
    }

    /// <summary>
    /// Lists the files of the folder the source is processed from, or takes the files that failed in earlier runs,
    /// and hands them over to be processed.
    /// </summary>
    private static async Task DoWork(ProcessingRun run, PhotoSource photoSource, DownloadServiceParameters parameters,
        IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        var retryFailed = run.Kind == RunKind.RetryFailed;
        var progress = parameters.Progress;

        await RunWithStatusAsync(run, serviceScopeFactory, () => progress,
            async (services, logger) =>
            {
                var fileStorage = services.GetRequiredService<IFileStorage>();
                var requestQueue = services.GetRequiredService<IMessageQueue<ProcessImageRequest>>();

                await using var downloadService = services.GetRequiredService<IPhotoSourceDownloadServiceFactory>()
                    .GetService(photoSource, parameters);

                // a retry downloads files the total already counts, the total of the previous run stays
                if (!retryFailed)
                {
                    progress.TotalCount = await downloadService.GetTotalFileCountAsync();
                }

                var downloadedFiles = retryFailed
                    ? downloadService.RetryFailedAsync(cancellationToken)
                    : downloadService.DownloadAsync(cancellationToken);

                await foreach (var downloadedFile in downloadedFiles)
                {
                    _ = downloadedFile.Processed.Task.ContinueWith(
                        a => CountProcessedFile(progress, a.Result, retryFailed), TaskContinuationOptions.ExecuteSynchronously);

                    string fileName;

                    try
                    {
                        fileName = await StoreDownloadedFileAsync(fileStorage, run, downloadedFile.FileInfo.ResourceName,
                            downloadedFile.FileContents);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Failed to store downloaded file {FileName}", downloadedFile.FileInfo.ResourceName);

                        downloadedFile.Processed.TrySetResult(ProcessingResult.Failed("Failed to store the downloaded file: " + e.Message));
                        continue;
                    }

                    await requestQueue.EnqueueAsync(CreateRequest(run, downloadedFile.FileInfo, fileName, downloadedFile.Processed),
                        cancellationToken);
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Every photo of the disk outside the folder it is processed from is listed, and the ones not saved yet are
    /// downloaded, several at a time, and processed as the photos of that folder are. A photo that fails is not
    /// saved, so the next run tries it again.
    /// </summary>
    private static async Task DoOtherFoldersWork(ProcessingRun run, string token, YandexDiskSettings settings,
        IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        var progress = new ProcessingProgress(0, 0);

        // the progress is replaced once the photos are listed, the reports take the current one
        await RunWithStatusAsync(run, serviceScopeFactory, () => progress,
            async (services, logger) =>
            {
                var photoService = services.GetRequiredService<IPhotoService>();
                var fileStorage = services.GetRequiredService<IFileStorage>();
                var requestQueue = services.GetRequiredService<IMessageQueue<ProcessImageRequest>>();
                var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();

                // Yandex.Disk tokens are not refreshed, the user authorizes again when the token has expired
                var apiClient = new ApiClient(token, httpClientFactory.CreateClient("yandexDiskClient"));
                var otherFolderPhotos = new YandexDiskOtherFolderPhotos(apiClient, logger);

                var sourceFolder = await YandexDiskDownloadService.GetSourceFolderAsync(apiClient, settings, logger, cancellationToken);
                var photos = await otherFolderPhotos.ListPhotosAsync(sourceFolder, cancellationToken);
                var savedExternalIds = await photoService.GetSavedExternalIdsAsync(run.UserId, run.SourceId,
                    photos.Select(a => a.ResourceId));

                // the photos saved by earlier runs count as processed, a run resumed carries on from them
                progress = new ProcessingProgress(savedExternalIds.Count, 0) { TotalCount = photos.Count };

                var photosToSave = photos.Where(a => !savedExternalIds.Contains(a.ResourceId)).ToList();

                logger.LogInformation("{PhotoCount} of {TotalCount} photos outside {SourceFolder} of {PhotoSourceName} to save for user {UserId}",
                    photosToSave.Count, photos.Count, sourceFolder, run.PhotoSourceName, run.UserId);

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(settings.MaxParallelDownloads, 1),
                    CancellationToken = cancellationToken
                };

                foreach (var chunk in photosToSave.Chunk(OtherFoldersSaveEvery))
                {
                    var processedTasks = new ConcurrentBag<Task<ProcessingResult>>();

                    await Parallel.ForEachAsync(chunk, parallelOptions, async (resource, token) =>
                    {
                        var processed = await EnqueuePhotoOrSkipAsync(otherFolderPhotos, fileStorage, requestQueue, logger, run,
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
                        CountProcessedFile(progress, result, retryFailed: false);
                    }
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Runs the work of a run in the background, with its own service scope: the scope of the request that started it
    /// is disposed by the time the run proceeds. The status of the run is reported when it starts, periodically while
    /// it goes, and when it has finished, done, stopped, paused or failed.
    /// </summary>
    private static async Task RunWithStatusAsync(
        ProcessingRun run,
        IServiceScopeFactory serviceScopeFactory,
        Func<ProcessingProgress> getProgress,
        Func<IServiceProvider, ILogger, Task> workAsync,
        CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var frontendNotificationService = scope.ServiceProvider.GetRequiredService<IFrontendNotificationService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PhotoSourceProcessingService>>();
        var applicationLifetime = scope.ServiceProvider.GetService<IHostApplicationLifetime>();

        var status = PhotoSourceStatus.Done;
        using var reportingCancellationTokenSource = new CancellationTokenSource();
        var reportingTask = Task.CompletedTask;

        Task ReportAsync(PhotoSourceStatus runStatus) =>
            ReportStatusAsync(serviceScopeFactory, logger, run, runStatus, getProgress());

        try
        {
            await ReportAsync(PhotoSourceStatus.InProgress);

            reportingTask = ReportStatusPeriodicallyAsync(() => ReportAsync(PhotoSourceStatus.InProgress),
                reportingCancellationTokenSource.Token);

            await workAsync(scope.ServiceProvider, logger);

            if (cancellationToken.IsCancellationRequested)
            {
                status = GetCancelledStatus(applicationLifetime);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            status = GetCancelledStatus(applicationLifetime);
        }
        catch (Exception e)
        {
            status = PhotoSourceStatus.Failed;

            if (run.Kind == RunKind.OtherFolders)
            {
                logger.LogError(e, "Processing of the other folders of {PhotoSourceName} for user {UserId} failed",
                    run.PhotoSourceName, run.UserId);

                await frontendNotificationService.SendOtherFoldersErrorAsync(run.UserId, run.SourceId, e.Message);
            }
            else
            {
                logger.LogError(e, "Processing of photo source {PhotoSourceName} for user {UserId} failed",
                    run.PhotoSourceName, run.UserId);

                await frontendNotificationService.SendErrorAsync(run.UserId, run.SourceId, e.Message);
            }
        }
        finally
        {
            await reportingCancellationTokenSource.CancelAsync();
            await reportingTask;

            await ReportAsync(status);
        }
    }

    /// <summary>
    /// Downloads the photo and hands it over to be processed. A photo that cannot be downloaded is skipped, unless
    /// Yandex.Disk no longer accepts the token, which fails every other photo as well.
    /// </summary>
    /// <returns>What the processing of the photo ends with, null when it is skipped.</returns>
    private static async Task<Task<ProcessingResult>?> EnqueuePhotoOrSkipAsync(YandexDiskOtherFolderPhotos otherFolderPhotos,
        IFileStorage fileStorage, IMessageQueue<ProcessImageRequest> requestQueue, ILogger logger, ProcessingRun run,
        Resource resource, CancellationToken cancellationToken)
    {
        try
        {
            var contents = await otherFolderPhotos.DownloadPhotoAsync(resource, cancellationToken);
            if (contents.Length == 0)
            {
                logger.LogWarning("{FileName} has been downloaded empty, skipping it", resource.Path);

                return null;
            }

            var fileName = await StoreDownloadedFileAsync(fileStorage, run, resource.Name, contents);
            var processed = new TaskCompletionSource<ProcessingResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            await requestQueue.EnqueueAsync(CreateRequest(run, CreateFileInfo(resource), fileName, processed), cancellationToken);

            return processed.Task;
        }
        catch (PhotoSourceException e) when (!e.IsAuthError)
        {
            // the error has been logged
            return null;
        }
        catch (IOException e)
        {
            logger.LogError(e, "Failed to store {FileName}", resource.Path);

            return null;
        }
    }

    /// <returns>The name the file is stored under.</returns>
    private static Task<string> StoreDownloadedFileAsync(IFileStorage fileStorage, ProcessingRun run, string name, byte[] contents)
    {
        // a path of its own per download: files of the same name, from another folder of the source or from the run
        // of another user, would overwrite each other while waiting to be processed
        return fileStorage.SaveAsync($"Bin/{run.UserId}/{run.SourceId}/{Guid.NewGuid():N}-{name}", contents);
    }

    private static ProcessImageRequest CreateRequest(ProcessingRun run, DownloadedFileInfo fileInfo, string fileName,
        TaskCompletionSource<ProcessingResult> processed)
    {
        return new ProcessImageRequest
        {
            DownloadedFileInfo = fileInfo,
            FileName = fileName,
            Sizes = run.Sizes,
            UserId = run.UserId,
            PhotoSourceId = run.SourceId,
            PhotoSourceName = run.PhotoSourceName,
            Processed = processed
        };
    }

    /// <summary>
    /// A file retried had been counted as failed, it is only counted again when it is saved this time; either way it
    /// counts as retried.
    /// </summary>
    private static void CountProcessedFile(ProcessingProgress progress, ProcessingResult result, bool retryFailed)
    {
        if (result.Succeeded)
        {
            if (retryFailed)
            {
                progress.FileRecovered();
            }
            else
            {
                progress.FileProcessed();
            }
        }
        else if (retryFailed)
        {
            progress.FileFailedAgain();
        }
        else
        {
            progress.FileFailed();
        }
    }

    /// <summary>
    /// A run is cancelled by the user stopping it or by the application stopping, which cancels every run.
    /// </summary>
    private static PhotoSourceStatus GetCancelledStatus(IHostApplicationLifetime? applicationLifetime)
    {
        return applicationLifetime?.ApplicationStopping.IsCancellationRequested == true
            ? PhotoSourceStatus.Paused
            : PhotoSourceStatus.Stopped;
    }

    /// <summary>
    /// Makes the report given every <see cref="StatusReportInterval"/> until the run has finished.
    /// </summary>
    private static async Task ReportStatusPeriodicallyAsync(Func<Task> reportAsync, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(StatusReportInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await reportAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // processing has finished
        }
    }

    /// <summary>
    /// Saves the status and counters to the database and sends them to the frontend: those of the folder the source
    /// is processed from, with how far a retry of the failed files has got, or those of the other folders. Failures
    /// are only logged, they must not stop the processing.
    /// </summary>
    private static async Task ReportStatusAsync(IServiceScopeFactory serviceScopeFactory, ILogger logger, ProcessingRun run,
        PhotoSourceStatus status, ProcessingProgress progress)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var userPhotoSourceService = scope.ServiceProvider.GetRequiredService<IUserPhotoSourceService>();
            var frontendNotificationService = scope.ServiceProvider.GetRequiredService<IFrontendNotificationService>();

            var userPhotoSourceStatus = new UserPhotoSourceStatus
            {
                UserId = run.UserId,
                PhotoSourceId = run.SourceId,
                Status = status,
                TotalCount = progress.TotalCount,
                ProcessedCount = progress.ProcessedCount,
                FailedCount = progress.FailedCount,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };

            switch (run.Kind)
            {
                case RunKind.OtherFolders:
                    await userPhotoSourceService.UpdateOtherFoldersStatusAsync(userPhotoSourceStatus);
                    await frontendNotificationService.SendOtherFoldersProgressAsync(userPhotoSourceStatus);
                    break;
                case RunKind.RetryFailed:
                    await userPhotoSourceService.UpdateUserPhotoStatusAsync(userPhotoSourceStatus);
                    await frontendNotificationService.SendProgressAsync(userPhotoSourceStatus,
                        new RetryProgress(progress.RetryTotalCount, progress.RetriedCount, progress.RecoveredCount));
                    break;
                default:
                    await userPhotoSourceService.UpdateUserPhotoStatusAsync(userPhotoSourceStatus);
                    await frontendNotificationService.SendProgressAsync(userPhotoSourceStatus);
                    break;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to report the status of the {RunKind} run of photo source {SourceId} for user {UserId}",
                run.Kind, run.SourceId, run.UserId);
        }
    }

    /// <summary>
    /// Continues the counters of the previous runs of the source, so that a run resumed after it was stopped
    /// carries on from the file it had reached instead of appearing to start over. A source that has never been
    /// processed has no counters to continue.
    /// </summary>
    private async Task<ProcessingProgress> CreateProgressAsync(long userId, long sourceId)
    {
        var status = await _userPhotoSourceService.GetUserPhotoStatusAsync(userId, sourceId);

        return new ProcessingProgress(status?.ProcessedCount ?? 0, status?.FailedCount ?? 0)
        {
            TotalCount = status?.TotalCount ?? 0
        };
    }

    private async Task<UserAuthResult> GetAuthResultAsync(long userId, long sourceId)
    {
        var authResult = await _userPhotoSourceService.GetAuthResultAsync(userId, sourceId);
        if (authResult == null || !authResult.IsValid)
        {
            throw new NotAuthorizedException("User is not authorized.");
        }

        return authResult;
    }

    private static bool IsYandexDisk(PhotoSource photoSource)
    {
        return Type.GetType(photoSource.ServiceFactoryImplementationType) == typeof(YandexDiskDownloadServiceFactory);
    }

    private static string GetTaskName(long userId, long sourceId, RunKind kind)
    {
        return kind == RunKind.OtherFolders
            ? $"OtherFolders-UserId={userId}-SourceId={sourceId}"
            : $"UserId={userId}-SourceId={sourceId}";
    }

    private enum RunKind
    {
        /// <summary>The files of the folder the source is processed from.</summary>
        Source,

        /// <summary>The files of the source that failed in earlier runs.</summary>
        RetryFailed,

        /// <summary>The photos of a Yandex.Disk source outside the folder it is processed from.</summary>
        OtherFolders
    }

    private sealed record ProcessingRun(long UserId, long SourceId, string PhotoSourceName, int[] Sizes, RunKind Kind);
}
