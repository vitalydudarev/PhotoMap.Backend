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

namespace PhotoMap.Api.Services;

public class PhotoSourceProcessingService : IPhotoSourceProcessingService
{
    private static readonly TimeSpan StatusReportInterval = TimeSpan.FromSeconds(2);

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

        throw new Exception("Unsupported command.");
    }

    public bool IsRunning(long userId, long sourceId)
    {
        return _backgroundTaskManager.IsRunning(GetTaskName(userId, sourceId));
    }

    private async Task<bool> StartAsync(long userId, long sourceId)
    {
        var taskName = GetTaskName(userId, sourceId);
        if (_backgroundTaskManager.IsRunning(taskName))
        {
            return false;
        }

        var authResult = await GetAuthResultAsync(userId, sourceId);
        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);
        var progress = await CreateProgressAsync(userId, sourceId);
        var parameters = CreateDownloadServiceParameters(photoSource, userId, sourceId, authResult, progress);
        var serviceScopeFactory = _serviceScopeFactory;
        var sizes = _photoProcessingSettings.Sizes;

        return _backgroundTaskManager.TryStartTask(taskName,
            cancellationToken => DoWork(photoSource, parameters, serviceScopeFactory, sizes, cancellationToken));
    }

    /// <summary>
    /// Runs in the background, with its own service scope: the scope of the request that started it is disposed
    /// by the time the run proceeds.
    /// </summary>
    private static async Task DoWork(
        PhotoSource photoSource,
        DownloadServiceParameters parameters,
        IServiceScopeFactory serviceScopeFactory,
        int[] sizes,
        CancellationToken cancellationToken)
    {
        var userId = parameters.UserId;
        var sourceId = parameters.SourceId;
        var photoSourceName = photoSource.Name;
        var progress = parameters.Progress;

        using var scope = serviceScopeFactory.CreateScope();
        var requestQueue = scope.ServiceProvider.GetRequiredService<IMessageQueue<ProcessImageRequest>>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var frontendNotificationService = scope.ServiceProvider.GetRequiredService<IFrontendNotificationService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PhotoSourceProcessingService>>();
        var applicationLifetime = scope.ServiceProvider.GetService<IHostApplicationLifetime>();

        var status = PhotoSourceStatus.Done;
        using var reportingCancellationTokenSource = new CancellationTokenSource();
        var reportingTask = Task.CompletedTask;
        IDownloadService? downloadService = null;

        try
        {
            await ReportStatusAsync(serviceScopeFactory, logger, userId, sourceId, PhotoSourceStatus.InProgress, progress);

            downloadService = scope.ServiceProvider.GetRequiredService<IPhotoSourceDownloadServiceFactory>()
                .GetService(photoSource, parameters);

            progress.TotalCount = await downloadService.GetTotalFileCountAsync();

            reportingTask = ReportStatusPeriodicallyAsync(serviceScopeFactory, logger, userId, sourceId, progress,
                reportingCancellationTokenSource.Token);

            await foreach (var downloadedFile in downloadService.DownloadAsync(cancellationToken))
            {
                _ = downloadedFile.Processed.Task.ContinueWith(a =>
                {
                    if (a.Result)
                    {
                        progress.FileProcessed();
                    }
                    else
                    {
                        progress.FileFailed();
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);

                string fileName;

                try
                {
                    // a path of its own per download: files of the same name, from another folder of the source
                    // or from the run of another user, would overwrite each other while waiting to be processed
                    var downloadedFilePath =
                        $"Bin/{userId}/{sourceId}/{Guid.NewGuid():N}-{downloadedFile.FileInfo.ResourceName}";

                    fileName = await fileStorage.SaveAsync(downloadedFilePath, downloadedFile.FileContents);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to store downloaded file {FileName}", downloadedFile.FileInfo.ResourceName);

                    downloadedFile.Processed.TrySetResult(false);
                    continue;
                }

                var request = new ProcessImageRequest
                {
                    DownloadedFileInfo = downloadedFile.FileInfo,
                    FileName = fileName,
                    Sizes = sizes,
                    UserId = userId,
                    PhotoSourceId = sourceId,
                    PhotoSourceName = photoSourceName,
                    Processed = downloadedFile.Processed
                };

                await requestQueue.EnqueueAsync(request, cancellationToken);
            }

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

            logger.LogError(e, "Processing of photo source {PhotoSourceName} for user {UserId} failed", photoSourceName, userId);

            await frontendNotificationService.SendErrorAsync(userId, sourceId, e.Message);
        }
        finally
        {
            await reportingCancellationTokenSource.CancelAsync();
            await reportingTask;

            if (downloadService != null)
            {
                await downloadService.DisposeAsync();
            }

            await ReportStatusAsync(serviceScopeFactory, logger, userId, sourceId, status, progress);
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

    private static async Task ReportStatusPeriodicallyAsync(
        IServiceScopeFactory serviceScopeFactory,
        ILogger logger,
        long userId,
        long sourceId,
        ProcessingProgress progress,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(StatusReportInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await ReportStatusAsync(serviceScopeFactory, logger, userId, sourceId, PhotoSourceStatus.InProgress, progress);
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
    private static async Task ReportStatusAsync(
        IServiceScopeFactory serviceScopeFactory,
        ILogger logger,
        long userId,
        long sourceId,
        PhotoSourceStatus status,
        ProcessingProgress progress)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var userPhotoSourceService = scope.ServiceProvider.GetRequiredService<IUserPhotoSourceService>();
            var frontendNotificationService = scope.ServiceProvider.GetRequiredService<IFrontendNotificationService>();

            var userPhotoSourceStatus = new UserPhotoSourceStatus
            {
                UserId = userId,
                PhotoSourceId = sourceId,
                Status = status,
                TotalCount = progress.TotalCount,
                ProcessedCount = progress.ProcessedCount,
                FailedCount = progress.FailedCount,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };

            await userPhotoSourceService.UpdateUserPhotoStatusAsync(userPhotoSourceStatus);
            await frontendNotificationService.SendProgressAsync(userPhotoSourceStatus);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to report the status of photo source {SourceId} for user {UserId}", sourceId, userId);
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

        return new ProcessingProgress(status?.ProcessedCount ?? 0, status?.FailedCount ?? 0);
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

    private static string GetTaskName(long userId, long sourceId)
    {
        return $"UserId={userId}-SourceId={sourceId}";
    }

    private static DownloadServiceParameters CreateDownloadServiceParameters(PhotoSource photoSource, long userId,
        long sourceId, UserAuthResult authResult, ProcessingProgress progress)
    {
        return new DownloadServiceParameters
        {
            UserId = userId,
            SourceId = sourceId,
            AuthResult = authResult,
            ClientId = photoSource.ClientAuthSettings.OAuthConfiguration.ClientId,
            Progress = progress
        };
    }
}
