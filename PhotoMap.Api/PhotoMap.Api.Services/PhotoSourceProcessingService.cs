using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

public class PhotoSourceProcessingService : IPhotoSourceProcessingService
{
    private readonly IPhotoSourceDownloadServiceFactory _downloadServiceFactory;
    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly IPhotoSourceService _photoSourceService;
    private readonly IFrontendNotificationService _frontendNotificationService;
    private readonly IBackgroundTaskManager _backgroundTaskManager;

    public PhotoSourceProcessingService(
        IPhotoSourceDownloadServiceFactory downloadServiceFactory,
        IUserPhotoSourceService userPhotoSourceService,
        IPhotoSourceService photoSourceService,
        IFrontendNotificationService frontendNotificationService,
        IBackgroundTaskManager backgroundTaskManager)
    {
        _downloadServiceFactory = downloadServiceFactory;
        _userPhotoSourceService = userPhotoSourceService;
        _photoSourceService = photoSourceService;
        _frontendNotificationService = frontendNotificationService;
        _backgroundTaskManager = backgroundTaskManager;
    }
    
    public async Task RunCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command)
    {
        // var taskName = $"UserId={userId}-SourceId={sourceId}";
        var taskName = string.Format("UserId={0}-SourceId={1}", userId, sourceId);
        
        if (command == PhotoSourceProcessingCommands.Start)
        {
            var token = await GetAuthTokenAsync(userId, sourceId);
            var downloadService = await CreateDownloadServiceAsync(sourceId);

            var totalFileCount = await downloadService.GetTotalFileCountAsync();

            var cancellationTokenSource = new CancellationTokenSource();
            
            _backgroundTaskManager.AddTask(taskName, () => DoWork(downloadService, _frontendNotificationService, userId, token, cancellationTokenSource.Token), cancellationTokenSource);
        }
        else if (command == PhotoSourceProcessingCommands.Stop)
        {
            _backgroundTaskManager.CancelTask(taskName);
            // take job and terminate it
        }
        else
        {
            throw new Exception("Unsupported command.");
        }
    }

    private static async Task DoWork(
        IDownloadService downloadService,
        IFrontendNotificationService frontendNotificationService,
        long userId,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var downloadedFileInfo in downloadService.DownloadAsync(userId, token, cancellationToken))
            {
                var name = downloadedFileInfo.ResourceName;
                await frontendNotificationService.SendProgressAsync(userId, 111, 49, 33);
                // send file to photo processing service
            }
        }
        catch (Exception e)
        {
            // TODO: handle auth exceptions
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            downloadService.Dispose();
        }
    }

    private async Task<string> GetAuthTokenAsync(long userId, long sourceId)
    {
        var authSettings = await _userPhotoSourceService.GetAuthResultAsync(userId, sourceId);
        if (authSettings?.Token == null || authSettings.TokenExpiresOn < DateTimeOffset.UtcNow)
        {
            throw new NotAuthorizedException("User is not authorized.");
        }

        return authSettings.Token;
    }

    private async Task<IDownloadService> CreateDownloadServiceAsync(long sourceId)
    {
        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);

        return _downloadServiceFactory.GetService(photoSource);
    }
}