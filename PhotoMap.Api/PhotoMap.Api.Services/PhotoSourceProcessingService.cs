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
using PhotoMap.Shared.Messaging;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services;

public class PhotoSourceProcessingService : IPhotoSourceProcessingService
{
    private static readonly TimeSpan StatusReportInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How many photos of the other uploads are downloaded and processed before the next ones start, which is how
    /// much of a stopped run is downloaded again when it resumes.
    /// </summary>
    private const int OtherUploadsSaveEvery = 100;

    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly IPhotoSourceService _photoSourceService;
    private readonly IBackgroundTaskManager _backgroundTaskManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly PhotoProcessingSettings _photoProcessingSettings;
    private readonly ProcessingAvailability _processingAvailability;

    public PhotoSourceProcessingService(
        IUserPhotoSourceService userPhotoSourceService,
        IPhotoSourceService photoSourceService,
        IBackgroundTaskManager backgroundTaskManager,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<PhotoProcessingSettings> photoProcessingSettings,
        ProcessingAvailability processingAvailability)
    {
        _userPhotoSourceService = userPhotoSourceService;
        _photoSourceService = photoSourceService;
        _backgroundTaskManager = backgroundTaskManager;
        _serviceScopeFactory = serviceScopeFactory;
        _photoProcessingSettings = photoProcessingSettings.Value;
        _processingAvailability = processingAvailability;
    }

    public async Task<bool> RunCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command)
    {
        EnsureAvailable(command);

        return command switch
        {
            PhotoSourceProcessingCommands.Start => await StartAsync(userId, sourceId, RunKind.Source),
            PhotoSourceProcessingCommands.RetryFailed => await StartAsync(userId, sourceId, RunKind.RetryFailed),
            PhotoSourceProcessingCommands.Sync => await StartAsync(userId, sourceId, RunKind.Sync),
            PhotoSourceProcessingCommands.Stop => Stop(userId, sourceId, RunKind.Source),
            _ => throw new Exception("Unsupported command.")
        };
    }

    public bool IsRunning(long userId, long sourceId)
    {
        return _backgroundTaskManager.IsRunning(GetTaskName(userId, sourceId, RunKind.Source));
    }

    public async Task<bool> SupportsOtherUploadsAsync(long sourceId)
    {
        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);

        return IsYandexDisk(photoSource);
    }

    public async Task<bool> RunOtherUploadsCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command)
    {
        EnsureAvailable(command);

        return command switch
        {
            PhotoSourceProcessingCommands.Start => await StartAsync(userId, sourceId, RunKind.OtherUploads),
            PhotoSourceProcessingCommands.RetryFailed => await StartAsync(userId, sourceId, RunKind.RetryFailedOtherUploads),
            PhotoSourceProcessingCommands.Sync => await StartAsync(userId, sourceId, RunKind.SyncOtherUploads),
            PhotoSourceProcessingCommands.Stop => Stop(userId, sourceId, RunKind.OtherUploads),
            _ => throw new Exception("Unsupported command.")
        };
    }

    public bool IsOtherUploadsRunning(long userId, long sourceId)
    {
        return _backgroundTaskManager.IsRunning(GetTaskName(userId, sourceId, RunKind.OtherUploads));
    }

    /// <summary>
    /// What a photo of the other uploads is saved with, as a photo of the folder the source is processed from is:
    /// where and when it was taken, and its hash, are read from the photo downloaded.
    /// </summary>
    public static DownloadedFileInfo CreateFileInfo(Resource resource)
    {
        // the time the photo was taken, when Yandex.Disk has read it from EXIF, otherwise the upload time
        var createdOn = resource.Exif?.DateTime ?? resource.PhotosliceTime ?? resource.Created;

        return new DownloadedFileInfo(resource.Name, resource.Path, createdOn, resource.ResourceId)
        {
            FromOtherUploads = true
        };
    }

    public int StopAllRuns()
    {
        // the runs of the camera uploads, and those of the other uploads
        return _backgroundTaskManager.CancelTasks(a => a.StartsWith(SourceTaskNamePrefix, StringComparison.Ordinal) ||
                                                       a.StartsWith(OtherUploadsTaskNamePrefix, StringComparison.Ordinal))
            .Count;
    }

    /// <summary>
    /// Stopping is always possible: it ends a run, and the work it hands over.
    /// </summary>
    private void EnsureAvailable(PhotoSourceProcessingCommands command)
    {
        if (command != PhotoSourceProcessingCommands.Stop)
        {
            _processingAvailability.EnsureAvailable();
        }
    }

    private bool Stop(long userId, long sourceId, RunKind kind)
    {
        // the run records the Stopped status when it has finished
        _backgroundTaskManager.CancelTask(GetTaskName(userId, sourceId, kind));
        return true;
    }

    /// <param name="kind">A retry of the failed files is the same task as a regular run of the files it retries, so
    /// that the two do not run at the same time. The other uploads run apart from the camera uploads.</param>
    private async Task<bool> StartAsync(long userId, long sourceId, RunKind kind)
    {
        var taskName = GetTaskName(userId, sourceId, kind);
        if (_backgroundTaskManager.IsRunning(taskName))
        {
            return false;
        }

        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);
        if (IsOtherUploads(kind) && !IsYandexDisk(photoSource))
        {
            throw new InvalidOperationException($"The other uploads of photo source {photoSource.Name} cannot be processed.");
        }

        var authResult = await GetAuthResultAsync(userId, sourceId);
        var uploadedBefore = kind is RunKind.Sync or RunKind.SyncOtherUploads
            ? await GetLastListedAtAsync(userId, sourceId, kind == RunKind.SyncOtherUploads)
            : null;
        var run = new ProcessingRun(userId, sourceId, photoSource.Name, _photoProcessingSettings.Sizes, kind,
            DateTimeOffset.UtcNow, uploadedBefore);
        var serviceScopeFactory = _serviceScopeFactory;

        Func<CancellationToken, Task> work;
        if (IsOtherUploads(kind))
        {
            var settings = new YandexDiskSettings();

            // a retry carries on the counters of the runs the photos failed in, a run lists the photos to count them
            var progress = kind == RunKind.RetryFailedOtherUploads
                ? await CreateOtherUploadsProgressAsync(userId, sourceId)
                : new ProcessingProgress(0, await CountFailedFilesAsync(userId, sourceId, fromOtherUploads: true));

            work = cancellationToken => DoOtherUploadsWork(run, authResult.Token, settings, progress, serviceScopeFactory,
                cancellationToken);
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
    /// Lists the files of the folder the source is processed from, or takes the files that failed in earlier runs, or
    /// syncs the files the runs have missed, and hands them over to be processed.
    /// </summary>
    private static async Task DoWork(ProcessingRun run, PhotoSource photoSource, DownloadServiceParameters parameters,
        IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        var retryFailed = run.Kind == RunKind.RetryFailed;
        var sync = run.Kind == RunKind.Sync;
        var progress = parameters.Progress;

        await RunWithStatusAsync(run, serviceScopeFactory, () => progress,
            async (services, logger) =>
            {
                var fileStorage = services.GetRequiredService<IFileStorage>();
                var requestSender = services.GetRequiredService<IMessageSender<ProcessImageRequest>>();
                var processingRequests = services.GetRequiredService<ProcessingRequests>();

                await using var downloadService = services.GetRequiredService<IPhotoSourceDownloadServiceFactory>()
                    .GetService(photoSource, parameters);

                // a retry or a sync downloads files the total already counts, the total of the previous run stays
                if (!retryFailed && !sync)
                {
                    progress.TotalCount = await downloadService.GetTotalFileCountAsync();
                }

                var downloadedFiles = retryFailed ? downloadService.RetryFailedAsync(cancellationToken)
                    : sync ? downloadService.SyncAsync(run.UploadedBefore, cancellationToken)
                    : downloadService.DownloadAsync(cancellationToken);

                await foreach (var downloadedFile in downloadedFiles.WithCancellation(cancellationToken))
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

                    await SendRequestAsync(requestSender, processingRequests, run, downloadedFile.FileInfo, fileName,
                        downloadedFile.Processed, cancellationToken);
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Every photo of the disk outside the folder it is processed from is listed, and the ones not saved yet are
    /// downloaded, several at a time, and processed as the photos of that folder are. A retry takes only the photos
    /// that failed in earlier runs instead, looked up by their paths again. The photos that fail are recorded, and
    /// the ones saved are no longer failed, as each chunk of them has been processed.
    /// </summary>
    private static async Task DoOtherUploadsWork(ProcessingRun run, string token, YandexDiskSettings settings,
        ProcessingProgress progress, IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        var retryFailed = run.Kind == RunKind.RetryFailedOtherUploads;

        // the progress of a run is replaced once the photos are listed, the reports take the current one
        await RunWithStatusAsync(run, serviceScopeFactory, () => progress,
            async (services, logger) =>
            {
                var photoService = services.GetRequiredService<IPhotoService>();
                var failedFileService = services.GetRequiredService<IFailedFileService>();
                var fileStorage = services.GetRequiredService<IFileStorage>();
                var requestSender = services.GetRequiredService<IMessageSender<ProcessImageRequest>>();
                var processingRequests = services.GetRequiredService<ProcessingRequests>();
                var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();

                // Yandex.Disk tokens are not refreshed, the user authorizes again when the token has expired
                var apiClient = new ApiClient(token, httpClientFactory.CreateClient("yandexDiskClient"));
                var otherUploads = new YandexDiskOtherUploads(apiClient, logger);

                List<OtherUpload> photosToSave;

                // the failed photos recorded are what the failed count is, however many times the runs have counted
                // them: a photo fails again in a chunk listed again after a stop, or in every run until it is retried
                async Task SyncFailedCountAsync() =>
                    progress.SetFailedCount(await failedFileService.CountAsync(run.UserId, run.SourceId, fromOtherUploads: true));

                if (retryFailed)
                {
                    var failedFiles = await failedFileService.GetOtherUploadsAsync(run.UserId, run.SourceId);

                    // the photos retried are counted as failed already, the counters of the earlier runs stay
                    progress.RetryTotalCount = failedFiles.Count;
                    photosToSave = failedFiles.Select(a => new OtherUpload(a.ExternalId, a.Path, a.FileName, null)).ToList();

                    logger.LogInformation("{PhotoCount} failed photos of the other uploads of {PhotoSourceName} to retry for user {UserId}",
                        photosToSave.Count, run.PhotoSourceName, run.UserId);
                }
                else
                {
                    var sourceFolder = await YandexDiskDownloadService.GetSourceFolderAsync(apiClient, settings, logger, cancellationToken);
                    var photos = await otherUploads.ListPhotosAsync(sourceFolder, cancellationToken);
                    var savedExternalIds = await photoService.GetSavedExternalIdsAsync(run.UserId, run.SourceId,
                        photos.Select(a => a.ResourceId));

                    // the photos saved by earlier runs count as processed, a run resumed carries on from them; the ones
                    // recorded as failed count as failed until they are saved
                    progress = new ProcessingProgress(savedExternalIds.Count,
                        await failedFileService.CountAsync(run.UserId, run.SourceId, fromOtherUploads: true))
                    {
                        TotalCount = photos.Count
                    };

                    var photosNotSaved = photos.Where(a => !savedExternalIds.Contains(a.ResourceId));

                    if (run.Kind == RunKind.SyncOtherUploads)
                    {
                        // a sync takes only the photos the runs have been through and missed, the ones uploaded since
                        // are left to the next run and the failed ones to Retry failed
                        var failedExternalIds = (await failedFileService.GetOtherUploadsAsync(run.UserId, run.SourceId))
                            .Select(a => a.ExternalId)
                            .ToHashSet();

                        photosNotSaved = run.UploadedBefore is { } uploadedBefore
                            ? photosNotSaved.Where(a => a.Created.ToUniversalTime() < uploadedBefore.UtcDateTime &&
                                                        !failedExternalIds.Contains(a.ResourceId))
                            : [];
                    }

                    photosToSave = photosNotSaved
                        .Select(a => new OtherUpload(a.ResourceId, a.Path, a.Name, a))
                        .ToList();

                    logger.LogInformation("{PhotoCount} of {TotalCount} photos outside {SourceFolder} of {PhotoSourceName} to save for user {UserId}",
                        photosToSave.Count, photos.Count, sourceFolder, run.PhotoSourceName, run.UserId);
                }

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(settings.MaxParallelDownloads, 1),
                    CancellationToken = cancellationToken
                };

                foreach (var chunk in photosToSave.Chunk(OtherUploadsSaveEvery))
                {
                    var attempts = new ConcurrentBag<OtherUploadAttempt>();

                    await Parallel.ForEachAsync(chunk, parallelOptions, async (photo, token) =>
                        attempts.Add(await EnqueuePhotoOrSkipAsync(otherUploads, fileStorage, requestSender,
                            processingRequests, logger, run, photo, token)));

                    // the photos of the chunk are saved before the next ones start, so that a stopped run loses no more
                    await Task.WhenAll(attempts.Where(a => a.Processed != null).Select(a => a.Processed!))
                        .WaitAsync(cancellationToken);

                    await RecordChunkAsync(failedFileService, progress, run, attempts, retryFailed);
                    await SyncFailedCountAsync();
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Counts the photos of a chunk, all of them processed or failed, and records the ones that failed, for a retry
    /// to take, while the ones saved are no longer failed.
    /// </summary>
    private static Task RecordChunkAsync(IFailedFileService failedFileService, ProcessingProgress progress,
        ProcessingRun run, IEnumerable<OtherUploadAttempt> attempts, bool retryFailed)
    {
        var failedFiles = new List<FailedFile>();
        var savedExternalIds = new List<string>();

        foreach (var attempt in attempts)
        {
            var result = attempt.Processed?.Result ?? ProcessingResult.Failed(attempt.Error ?? "Downloading failed.");

            CountProcessedFile(progress, result, retryFailed);

            if (result.Succeeded)
            {
                savedExternalIds.Add(attempt.Photo.ExternalId);
            }
            else
            {
                failedFiles.Add(new FailedFile
                {
                    ExternalId = attempt.Photo.ExternalId,
                    Path = attempt.Photo.Path,
                    FileName = attempt.Photo.FileName,
                    Stage = attempt.Stage,
                    Error = result.Error ?? "Processing failed.",
                    FailedAt = DateTimeOffset.UtcNow,
                    FromOtherUploads = true
                });
            }
        }

        return failedFileService.RecordAsync(run.UserId, run.SourceId, failedFiles, savedExternalIds);
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

            if (IsOtherUploads(run.Kind))
            {
                logger.LogError(e, "Processing of the other uploads of {PhotoSourceName} for user {UserId} failed",
                    run.PhotoSourceName, run.UserId);

                await frontendNotificationService.SendOtherUploadsErrorAsync(run.UserId, run.SourceId, e.Message);
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
    /// Downloads the photo, looked up by its path first when it is retried, and hands it over to be processed. A photo
    /// that cannot be downloaded or stored is skipped, unless Yandex.Disk no longer accepts the token, which fails
    /// every other photo as well.
    /// </summary>
    private static async Task<OtherUploadAttempt> EnqueuePhotoOrSkipAsync(YandexDiskOtherUploads otherUploads,
        IFileStorage fileStorage, IMessageSender<ProcessImageRequest> requestSender, ProcessingRequests processingRequests,
        ILogger logger, ProcessingRun run, OtherUpload photo, CancellationToken cancellationToken)
    {
        byte[] contents;
        Resource resource;

        try
        {
            resource = photo.Resource ?? await GetFailedPhotoAsync(otherUploads, photo, cancellationToken);
            contents = await otherUploads.DownloadPhotoAsync(resource, cancellationToken);
        }
        catch (PhotoSourceException e) when (!e.IsAuthError)
        {
            // the error has been logged
            return OtherUploadAttempt.Failed(photo, FileFailureStage.Download, e.Message);
        }

        if (contents.Length == 0)
        {
            logger.LogWarning("{FileName} has been downloaded empty, skipping it", photo.Path);

            return OtherUploadAttempt.Failed(photo, FileFailureStage.Download, "The photo has been downloaded empty.");
        }

        string fileName;

        try
        {
            fileName = await StoreDownloadedFileAsync(fileStorage, run, resource.Name, contents);
        }
        catch (IOException e)
        {
            logger.LogError(e, "Failed to store {FileName}", photo.Path);

            return OtherUploadAttempt.Failed(photo, FileFailureStage.Processing, "Failed to store the downloaded file: " + e.Message);
        }

        var processed = new TaskCompletionSource<ProcessingResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        await SendRequestAsync(requestSender, processingRequests, run, CreateFileInfo(resource), fileName, processed,
            cancellationToken);

        return new OtherUploadAttempt(photo, processed.Task, FileFailureStage.Processing, null);
    }

    /// <exception cref="PhotoSourceException">The photo could not be looked up.</exception>
    private static Task<Resource> GetFailedPhotoAsync(YandexDiskOtherUploads otherUploads, OtherUpload photo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(photo.Path))
        {
            throw new YandexDiskException("Yandex.Disk files are downloaded by path, the path is missing.");
        }

        return otherUploads.GetPhotoAsync(photo.Path, cancellationToken);
    }

    /// <returns>The name the file is stored under.</returns>
    private static Task<string> StoreDownloadedFileAsync(IFileStorage fileStorage, ProcessingRun run, string name, byte[] contents)
    {
        // a path of its own per download: files of the same name, from another folder of the source or from the run
        // of another user, would overwrite each other while waiting to be processed
        return fileStorage.SaveAsync($"Bin/{run.UserId}/{run.SourceId}/{Guid.NewGuid():N}-{name}", contents);
    }

    /// <summary>
    /// Hands the downloaded file over to the worker: <paramref name="processed"/> is completed when its result
    /// comes. Waits until the transport has the request: Kafka has acknowledged it, or there is room in the queue.
    /// </summary>
    /// <exception cref="MessagePublishException">The request has not been delivered, e.g. Kafka has been down for
    /// longer than the producer waits; the run fails, to be resumed.</exception>
    private static async Task SendRequestAsync(IMessageSender<ProcessImageRequest> requestSender,
        ProcessingRequests processingRequests, ProcessingRun run, DownloadedFileInfo fileInfo, string fileName,
        TaskCompletionSource<ProcessingResult> processed, CancellationToken cancellationToken)
    {
        var requestId = processingRequests.Register(processed);

        try
        {
            await requestSender.SendAsync(CreateRequest(run, fileInfo, fileName, requestId), cancellationToken);
        }
        catch
        {
            processingRequests.Forget(requestId);
            throw;
        }
    }

    /// <param name="requestId">From <see cref="ProcessingRequests.Register"/>, for the result to be matched back to
    /// what waits for it.</param>
    private static ProcessImageRequest CreateRequest(ProcessingRun run, DownloadedFileInfo fileInfo, string fileName,
        Guid requestId)
    {
        return new ProcessImageRequest
        {
            DownloadedFileInfo = fileInfo,
            FileName = fileName,
            Sizes = run.Sizes,
            UserId = run.UserId,
            PhotoSourceId = run.SourceId,
            PhotoSourceName = run.PhotoSourceName,
            RequestId = requestId
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
    /// is processed from, with how far a retry of the failed files has got, or those of the other uploads. Failures
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
                LastUpdatedAt = DateTimeOffset.UtcNow,
                // the files uploaded before a listing run that has finished started are the ones a sync checks
                LastListedAt = status == PhotoSourceStatus.Done && run.Kind is RunKind.Source or RunKind.OtherUploads
                    ? run.StartedAt
                    : null
            };

            switch (run.Kind)
            {
                case RunKind.OtherUploads:
                case RunKind.SyncOtherUploads:
                    await userPhotoSourceService.UpdateOtherUploadsStatusAsync(userPhotoSourceStatus);
                    await frontendNotificationService.SendOtherUploadsProgressAsync(userPhotoSourceStatus);
                    break;
                case RunKind.RetryFailedOtherUploads:
                    await userPhotoSourceService.UpdateOtherUploadsStatusAsync(userPhotoSourceStatus);
                    await frontendNotificationService.SendOtherUploadsProgressAsync(userPhotoSourceStatus,
                        new RetryProgress(progress.RetryTotalCount, progress.RetriedCount, progress.RecoveredCount));
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
    /// processed has no counters to continue. The failed count is the files recorded as failed, which the counter
    /// saved may have run ahead of.
    /// </summary>
    private async Task<ProcessingProgress> CreateProgressAsync(long userId, long sourceId)
    {
        var status = await _userPhotoSourceService.GetUserPhotoStatusAsync(userId, sourceId);

        return new ProcessingProgress(status?.ProcessedCount ?? 0,
            await CountFailedFilesAsync(userId, sourceId, fromOtherUploads: false))
        {
            TotalCount = status?.TotalCount ?? 0
        };
    }

    /// <summary>
    /// Continues the counters of the earlier runs of the other uploads, which a retry of their failed photos changes.
    /// </summary>
    private async Task<ProcessingProgress> CreateOtherUploadsProgressAsync(long userId, long sourceId)
    {
        var status = await _userPhotoSourceService.GetOtherUploadsStatusAsync(userId, sourceId);

        return new ProcessingProgress(status?.ProcessedCount ?? 0,
            await CountFailedFilesAsync(userId, sourceId, fromOtherUploads: true))
        {
            TotalCount = status?.TotalCount ?? 0
        };
    }

    /// <summary>
    /// The files of the source recorded as failed, each once however many times it has failed: the failed count of
    /// its processing. The failed files are read with a scope of their own, the service outlives the requests.
    /// </summary>
    private async Task<int> CountFailedFilesAsync(long userId, long sourceId, bool fromOtherUploads)
    {
        using var scope = _serviceScopeFactory.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IFailedFileService>()
            .CountAsync(userId, sourceId, fromOtherUploads);
    }

    /// <summary>
    /// When the last run listing the camera uploads, or the other uploads, that has finished started. The statuses
    /// saved before it was kept have only the time they were last updated at; null when the uploads have never been
    /// processed.
    /// </summary>
    private async Task<DateTimeOffset?> GetLastListedAtAsync(long userId, long sourceId, bool otherUploads)
    {
        var status = otherUploads
            ? await _userPhotoSourceService.GetOtherUploadsStatusAsync(userId, sourceId)
            : await _userPhotoSourceService.GetUserPhotoStatusAsync(userId, sourceId);

        return status?.LastListedAt ?? status?.LastUpdatedAt;
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

    private const string SourceTaskNamePrefix = "UserId=";
    private const string OtherUploadsTaskNamePrefix = "OtherUploads-";

    private static string GetTaskName(long userId, long sourceId, RunKind kind)
    {
        return IsOtherUploads(kind)
            ? $"{OtherUploadsTaskNamePrefix}UserId={userId}-SourceId={sourceId}"
            : $"{SourceTaskNamePrefix}{userId}-SourceId={sourceId}";
    }

    private enum RunKind
    {
        /// <summary>The files of the folder the source is processed from.</summary>
        Source,

        /// <summary>The files of the source that failed in earlier runs.</summary>
        RetryFailed,

        /// <summary>The photos of a Yandex.Disk source outside the folder it is processed from.</summary>
        OtherUploads,

        /// <summary>The photos of the other uploads that failed in earlier runs.</summary>
        RetryFailedOtherUploads,

        /// <summary>The files of the source the runs have been through and missed.</summary>
        Sync,

        /// <summary>The photos of the other uploads the runs have been through and missed.</summary>
        SyncOtherUploads
    }

    private static bool IsOtherUploads(RunKind kind)
    {
        return kind is RunKind.OtherUploads or RunKind.RetryFailedOtherUploads or RunKind.SyncOtherUploads;
    }

    /// <summary>
    /// A photo of the other uploads to save: listed, or failed in an earlier run, when it is looked up by its path.
    /// </summary>
    private sealed record OtherUpload(string ExternalId, string? Path, string FileName, Resource? Resource);

    /// <param name="Processed">What the processing of the photo ends with, null when it has failed before.</param>
    /// <param name="Stage">What the photo fails at, when it fails.</param>
    /// <param name="Error">Why the photo has failed before being processed.</param>
    private sealed record OtherUploadAttempt(OtherUpload Photo, Task<ProcessingResult>? Processed, FileFailureStage Stage,
        string? Error)
    {
        public static OtherUploadAttempt Failed(OtherUpload photo, FileFailureStage stage, string error) =>
            new(photo, null, stage, error);
    }

    /// <param name="StartedAt">When the run started, which a listing run that finishes saves for the syncs.</param>
    /// <param name="UploadedBefore">For a sync, the files uploaded before this time are the ones it checks.</param>
    private sealed record ProcessingRun(long UserId, long SourceId, string PhotoSourceName, int[] Sizes, RunKind Kind,
        DateTimeOffset StartedAt, DateTimeOffset? UploadedBefore);
}
