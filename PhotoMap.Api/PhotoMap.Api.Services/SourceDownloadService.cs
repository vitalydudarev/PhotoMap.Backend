using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

public class SourceDownloadService : ISourceDownloadService
{
    private static readonly TimeSpan StatusReportInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The most outcomes of downloads recorded at a time. Fewer are recorded when no more have come in the meantime,
    /// so an outcome waits only for the ones recorded before it.
    /// </summary>
    private const int MaxRecordBatchSize = 50;

    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly IPhotoSourceService _photoSourceService;
    private readonly IBackgroundTaskManager _backgroundTaskManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IFileStorage _fileStorage;
    private readonly IFailedDownloadService _failedDownloadService;
    private readonly ISourceFilesFactory _sourceFilesFactory;
    private readonly SourceDownloadStatuses _statuses;

    public SourceDownloadService(
        IUserPhotoSourceService userPhotoSourceService,
        IPhotoSourceService photoSourceService,
        IBackgroundTaskManager backgroundTaskManager,
        IServiceScopeFactory serviceScopeFactory,
        IFileStorage fileStorage,
        IFailedDownloadService failedDownloadService,
        ISourceFilesFactory sourceFilesFactory,
        SourceDownloadStatuses statuses)
    {
        _userPhotoSourceService = userPhotoSourceService;
        _photoSourceService = photoSourceService;
        _backgroundTaskManager = backgroundTaskManager;
        _serviceScopeFactory = serviceScopeFactory;
        _fileStorage = fileStorage;
        _failedDownloadService = failedDownloadService;
        _sourceFilesFactory = sourceFilesFactory;
        _statuses = statuses;
    }

    public async Task<bool> SupportsDownloadsAsync(long sourceId, SourceDownloadKind kind)
    {
        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);

        return Supports(photoSource, kind);
    }

    public Task<bool> StartAsync(long userId, long sourceId, SourceDownloadContent content)
    {
        ValidateContent(content);

        return StartAsync(userId, sourceId, SourceDownloadKind.ProcessingFolder, content, folder: null, retryFailed: false);
    }

    public Task<bool> StartFolderAsync(long userId, long sourceId, string folder, SourceDownloadContent content)
    {
        ValidateContent(content);

        var normalizedFolder = YandexDiskPaths.NormalizeFolder(folder)
            ?? throw new ArgumentException($"{folder} is not a folder that can be downloaded from.", nameof(folder));

        return StartAsync(userId, sourceId, SourceDownloadKind.ChosenFolder, content, normalizedFolder, retryFailed: false);
    }

    public Task<bool> RetryFailedAsync(long userId, long sourceId, SourceDownloadKind kind)
    {
        return StartAsync(userId, sourceId, kind, SourceDownloadContent.All, folder: null, retryFailed: true);
    }

    public Task<IReadOnlyList<FailedDownload>> GetFailedDownloadsAsync(long userId, long sourceId, SourceDownloadKind kind)
    {
        return _failedDownloadService.GetAsync(userId, sourceId, kind);
    }

    public void Stop(long userId, long sourceId, SourceDownloadKind kind)
    {
        // the run records the Stopped status when it has finished
        _backgroundTaskManager.CancelTask(GetTaskName(userId, sourceId, kind));
    }

    public bool IsRunning(long userId, long sourceId, SourceDownloadKind kind)
    {
        return _backgroundTaskManager.IsRunning(GetTaskName(userId, sourceId, kind));
    }

    public SourceDownloadStatus? GetStatus(long userId, long sourceId, SourceDownloadKind kind)
    {
        return _statuses.Get(userId, sourceId, kind);
    }

    public async Task<string> GetTargetFolderAsync(long userId, long sourceId, SourceDownloadKind kind)
    {
        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);

        return _fileStorage.GetFullPath(GetTargetFolder(photoSource.Name, userId, kind));
    }

    private static void ValidateContent(SourceDownloadContent content)
    {
        if (content is not (SourceDownloadContent.Photos or SourceDownloadContent.Videos or SourceDownloadContent.All))
        {
            throw new ArgumentOutOfRangeException(nameof(content), content, "Photos, videos or both are downloaded.");
        }
    }

    private bool Supports(PhotoSource photoSource, SourceDownloadKind kind)
    {
        return kind == SourceDownloadKind.ChosenFolder
            ? _sourceFilesFactory.SupportsChosenFolders(photoSource)
            : _sourceFilesFactory.Supports(photoSource);
    }

    /// <param name="folder">The folder a chosen folder download lists, null for any other.</param>
    /// <param name="retryFailed">Download the files that failed before rather than list the folder. The run is the
    /// same task as a regular one of its kind, so that the two do not run at the same time.</param>
    private async Task<bool> StartAsync(long userId, long sourceId, SourceDownloadKind kind, SourceDownloadContent content,
        string? folder, bool retryFailed)
    {
        var taskName = GetTaskName(userId, sourceId, kind);
        if (_backgroundTaskManager.IsRunning(taskName))
        {
            return false;
        }

        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);
        if (!Supports(photoSource, kind))
        {
            throw new InvalidOperationException(kind == SourceDownloadKind.ChosenFolder
                ? $"The folders of photo source {photoSource.Name} cannot be chosen to download from."
                : $"The files of photo source {photoSource.Name} cannot be downloaded.");
        }

        var authResult = await _userPhotoSourceService.GetAuthResultAsync(userId, sourceId);
        if (authResult == null || !authResult.IsValid)
        {
            throw new NotAuthorizedException("User is not authorized.");
        }

        var run = new DownloadRun(userId, sourceId, photoSource, authResult, kind, content, folder, retryFailed);
        var serviceScopeFactory = _serviceScopeFactory;

        return _backgroundTaskManager.TryStartTask(taskName, cancellationToken => DoWork(run, serviceScopeFactory, cancellationToken));
    }

    /// <summary>
    /// Runs in the background, with its own service scope: the scope of the request that started it is disposed
    /// by the time the run proceeds. The files of the folder, or the ones that failed before, are told apart into
    /// the ones downloaded whole already and the missing ones, which are downloaded several at a time. The files
    /// that fail are recorded, and the ones downloaded are no longer failed.
    /// </summary>
    private static async Task DoWork(DownloadRun run, IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var failedDownloadService = scope.ServiceProvider.GetRequiredService<IFailedDownloadService>();
        var frontendNotificationService = scope.ServiceProvider.GetRequiredService<IFrontendNotificationService>();
        var statuses = scope.ServiceProvider.GetRequiredService<SourceDownloadStatuses>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SourceDownloadService>>();
        var sourceFilesFactory = scope.ServiceProvider.GetRequiredService<ISourceFilesFactory>();
        var applicationLifetime = scope.ServiceProvider.GetService<IHostApplicationLifetime>();

        var progress = new ProcessingProgress(0, 0);
        var alreadyDownloadedCount = 0;
        var status = PhotoSourceStatus.Done;
        using var reportingCancellationTokenSource = new CancellationTokenSource();
        var reportingTask = Task.CompletedTask;

        try
        {
            await ReportStatusAsync(statuses, frontendNotificationService, logger, run, PhotoSourceStatus.InProgress, progress,
                alreadyDownloadedCount);

            using var sourceFiles = sourceFilesFactory.Create(run.PhotoSource, run.AuthResult);

            var files = run.RetryFailed
                ? (await failedDownloadService.GetAsync(run.UserId, run.SourceId, run.Kind)).Select(ToSourceFile).ToList()
                : await sourceFiles.ListAsync(run.Content, run.Folder, cancellationToken);

            var (downloadedFiles, missingFiles) = SplitDownloaded(fileStorage, run, files);
            alreadyDownloadedCount = downloadedFiles.Count;

            logger.LogInformation(
                "{DownloadedCount} of {TotalCount} {Files} of {Folder} of {PhotoSourceName} for user {UserId} are downloaded already, {MissingCount} missing",
                downloadedFiles.Count, files.Count, run.RetryFailed ? "failed files" : "files", run.Folder ?? run.Kind.ToString(),
                run.PhotoSource.Name, run.UserId, missingFiles.Count);

            // a file found whole is no longer failed, whatever it failed with before
            await failedDownloadService.RecordAsync(run.UserId, run.SourceId, run.Kind, [],
                downloadedFiles.Select(a => GetKey(run, a)).ToList());

            progress = new ProcessingProgress(downloadedFiles.Count, 0) { TotalCount = files.Count };

            reportingTask = ReportStatusPeriodicallyAsync(statuses, frontendNotificationService, logger, run, progress,
                alreadyDownloadedCount, reportingCancellationTokenSource.Token);

            await DownloadAllAsync(sourceFiles, fileStorage, failedDownloadService, logger, run, missingFiles, progress,
                cancellationToken);
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

            logger.LogError(e, "Downloading the files of {PhotoSourceName} for user {UserId} failed", run.PhotoSource.Name,
                run.UserId);

            await frontendNotificationService.SendDownloadErrorAsync(run.UserId, run.SourceId, run.Kind, e.Message);
        }
        finally
        {
            await reportingCancellationTokenSource.CancelAsync();
            await reportingTask;

            await ReportStatusAsync(statuses, frontendNotificationService, logger, run, status, progress, alreadyDownloadedCount);
        }
    }

    /// <summary>
    /// Downloads the files several at a time, each worker taking the next file as soon as it has finished one: a
    /// large file holds up only the worker downloading it. The outcomes are recorded by a recorder of their own, one
    /// at a time, as the database context of the run is not thread safe.
    /// </summary>
    /// <exception cref="OperationCanceledException">The download has been stopped. The outcomes of the files
    /// finished before have been recorded.</exception>
    private static async Task DownloadAllAsync(ISourceFiles sourceFiles, IFileStorage fileStorage,
        IFailedDownloadService failedDownloadService, ILogger logger, DownloadRun run, IReadOnlyCollection<SourceFile> files,
        ProcessingProgress progress, CancellationToken cancellationToken)
    {
        var outcomes = Channel.CreateUnbounded<DownloadOutcome>(new UnboundedChannelOptions { SingleReader = true });

        // the recorder failing stops the workers, there is no point in downloading what cannot be recorded
        using var recordingFailedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var recordingTask = RecordOutcomesAsync(failedDownloadService, run, outcomes.Reader, recordingFailedCancellationTokenSource);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = sourceFiles.MaxParallelDownloads,
            CancellationToken = recordingFailedCancellationTokenSource.Token
        };

        try
        {
            await Parallel.ForEachAsync(files, parallelOptions, async (file, token) =>
            {
                var error = await DownloadOrSkipAsync(sourceFiles, fileStorage, logger, run, file, token);
                if (error == null)
                {
                    progress.FileProcessed();
                }
                else
                {
                    progress.FileFailed();
                }

                outcomes.Writer.TryWrite(new DownloadOutcome(file, error));
            });
        }
        finally
        {
            // recorded however the download has ended, the files finished before a stop are not lost; a recorder
            // that has failed fails the download with its error, rather than with the workers it has stopped
            outcomes.Writer.Complete();
            await recordingTask;
        }
    }

    /// <summary>
    /// Records the outcomes as they come, as many as have come at a time, until the workers have finished.
    /// </summary>
    private static async Task RecordOutcomesAsync(IFailedDownloadService failedDownloadService, DownloadRun run,
        ChannelReader<DownloadOutcome> outcomes, CancellationTokenSource recordingFailedCancellationTokenSource)
    {
        try
        {
            var batch = new List<DownloadOutcome>(MaxRecordBatchSize);

            // not cancelled with the download: the outcomes that have come before a stop are recorded all the same
            while (await outcomes.WaitToReadAsync())
            {
                batch.Clear();

                while (batch.Count < MaxRecordBatchSize && outcomes.TryRead(out var outcome))
                {
                    batch.Add(outcome);
                }

                var failedDownloads = batch
                    .Where(a => a.Error != null)
                    .Select(a => ToFailedDownload(run, a.File, a.Error!))
                    .ToList();
                var downloadedFiles = batch.Where(a => a.Error == null).Select(a => GetKey(run, a.File)).ToList();

                await failedDownloadService.RecordAsync(run.UserId, run.SourceId, run.Kind, failedDownloads, downloadedFiles);
            }
        }
        catch
        {
            await recordingFailedCancellationTokenSource.CancelAsync();
            throw;
        }
    }

    /// <summary>
    /// Tells the files downloaded whole already, of the size the source tells, from the missing ones: a file that is
    /// not in the file storage, or only in part.
    /// </summary>
    private static (List<SourceFile> Downloaded, List<SourceFile> Missing) SplitDownloaded(IFileStorage fileStorage,
        DownloadRun run, IEnumerable<SourceFile> files)
    {
        var downloaded = new List<SourceFile>();
        var missing = new List<SourceFile>();

        foreach (var file in files)
        {
            (IsDownloaded(fileStorage, run, file) ? downloaded : missing).Add(file);
        }

        return (downloaded, missing);
    }

    /// <summary>
    /// A file whose path cannot be kept in the file storage counts as missing, its download fails with the reason.
    /// </summary>
    private static bool IsDownloaded(IFileStorage fileStorage, DownloadRun run, SourceFile file)
    {
        try
        {
            return fileStorage.GetSize(GetFilePath(run, file)) == file.Size;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Downloads the file into the file storage. A file that cannot be downloaded is skipped, unless the source no
    /// longer accepts the access token, which fails every other file as well.
    /// </summary>
    /// <returns>null when the file has been downloaded, otherwise what it failed with.</returns>
    private static async Task<string?> DownloadOrSkipAsync(ISourceFiles sourceFiles, IFileStorage fileStorage, ILogger logger,
        DownloadRun run, SourceFile file, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Started downloading {FileName}", file.Name);

            await fileStorage.SaveAsync(GetFilePath(run, file),
                (stream, token) => sourceFiles.CopyToAsync(file, stream, token), cancellationToken);

            logger.LogInformation("Finished downloading {FileName}", file.Name);

            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (!sourceFiles.IsAuthError(e))
        {
            logger.LogError(e, "Failed to download {FileName}", file.Name);

            return e.Message;
        }
    }

    private static SourceFile ToSourceFile(FailedDownload failedDownload)
    {
        return new SourceFile(failedDownload.FileName, failedDownload.Path, failedDownload.Size, failedDownload.Content);
    }

    private static FailedDownload ToFailedDownload(DownloadRun run, SourceFile file, string error)
    {
        return new FailedDownload
        {
            UserId = run.UserId,
            PhotoSourceId = run.SourceId,
            Kind = run.Kind,
            Folder = GetKey(run, file).Folder,
            FileName = file.Name,
            Path = file.Path,
            Size = file.Size,
            Content = file.Content,
            Error = error,
            FailedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// The files of a source are downloaded next to its video previews: those of the folder its photos are processed
    /// from to <c>Yandex.Disk/1/downloads</c>, those of chosen folders to <c>Yandex.Disk/1/folders</c>.
    /// </summary>
    private static string GetTargetFolder(string photoSourceName, long userId, SourceDownloadKind kind)
    {
        return Path.Combine(photoSourceName, userId.ToString(), kind == SourceDownloadKind.ChosenFolder ? "folders" : "downloads");
    }

    /// <summary>
    /// The files of the folder the photos are processed from come from that single folder, where their names are
    /// unique. The files of chosen folders keep the path of their folder, so that the files of the same name in
    /// different folders do not overwrite each other: <c>Yandex.Disk/1/folders/Photos/2020/IMG_0001.jpg</c>.
    /// </summary>
    private static string GetFilePath(DownloadRun run, SourceFile file)
    {
        var targetFolder = GetTargetFolder(run.PhotoSource.Name, run.UserId, run.Kind);

        return run.Kind == SourceDownloadKind.ChosenFolder
            ? YandexDiskPaths.ToStoragePath(targetFolder, file.Path)
            : Path.Combine(targetFolder, file.Name);
    }

    /// <summary>
    /// The file, by its folder and name, as its failures are recorded.
    /// </summary>
    private static DownloadedFileKey GetKey(DownloadRun run, SourceFile file)
    {
        return new DownloadedFileKey(run.Kind == SourceDownloadKind.ChosenFolder ? YandexDiskPaths.GetFolder(file.Path) : "", file.Name);
    }

    private static async Task ReportStatusPeriodicallyAsync(SourceDownloadStatuses statuses,
        IFrontendNotificationService frontendNotificationService, ILogger logger, DownloadRun run, ProcessingProgress progress,
        int alreadyDownloadedCount, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(StatusReportInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await ReportStatusAsync(statuses, frontendNotificationService, logger, run, PhotoSourceStatus.InProgress, progress,
                    alreadyDownloadedCount);
            }
        }
        catch (OperationCanceledException)
        {
            // the download has finished
        }
    }

    /// <summary>
    /// Keeps the status and counters and sends them to the frontend. Failures are only logged, they must not stop
    /// the download.
    /// </summary>
    private static async Task ReportStatusAsync(SourceDownloadStatuses statuses,
        IFrontendNotificationService frontendNotificationService, ILogger logger, DownloadRun run, PhotoSourceStatus status,
        ProcessingProgress progress, int alreadyDownloadedCount)
    {
        var lastUpdatedAt = DateTimeOffset.UtcNow;

        statuses.Set(run.UserId, run.SourceId, run.Kind, new SourceDownloadStatus(status, run.Content, run.Folder, run.RetryFailed,
            progress.TotalCount, progress.ProcessedCount, alreadyDownloadedCount, progress.FailedCount, lastUpdatedAt));

        try
        {
            await frontendNotificationService.SendDownloadProgressAsync(new UserPhotoSourceStatus
            {
                UserId = run.UserId,
                PhotoSourceId = run.SourceId,
                Status = status,
                TotalCount = progress.TotalCount,
                ProcessedCount = progress.ProcessedCount,
                FailedCount = progress.FailedCount,
                LastUpdatedAt = lastUpdatedAt
            }, run.Kind);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to report the download of photo source {SourceId} for user {UserId}", run.SourceId,
                run.UserId);
        }
    }

    /// <summary>
    /// The downloads of each kind are a task of their own, they run alongside each other.
    /// </summary>
    private static string GetTaskName(long userId, long sourceId, SourceDownloadKind kind)
    {
        return kind == SourceDownloadKind.ChosenFolder
            ? $"FolderDownload-UserId={userId}-SourceId={sourceId}"
            : $"Download-UserId={userId}-SourceId={sourceId}";
    }

    /// <param name="Error">What the file failed with, null when it has been downloaded.</param>
    private sealed record DownloadOutcome(SourceFile File, string? Error);

    /// <param name="Folder">The folder a chosen folder download lists, null for any other.</param>
    private sealed record DownloadRun(long UserId, long SourceId, PhotoSource PhotoSource, UserAuthResult AuthResult,
        SourceDownloadKind Kind, SourceDownloadContent Content, string? Folder, bool RetryFailed);
}
