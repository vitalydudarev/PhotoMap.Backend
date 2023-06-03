using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

public class PhotoSourceProcessingService
{
    private readonly IPhotoSourceDownloadServiceFactory _downloadServiceFactory;
    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly IPhotoSourceService _photoSourceService;
    private readonly IBackgroundTaskManager _backgroundTaskManager;

    public PhotoSourceProcessingService(
        IPhotoSourceDownloadServiceFactory downloadServiceFactory,
        IUserPhotoSourceService userPhotoSourceService,
        IPhotoSourceService photoSourceService,
        IBackgroundTaskManager backgroundTaskManager)
    {
        _downloadServiceFactory = downloadServiceFactory;
        _userPhotoSourceService = userPhotoSourceService;
        _photoSourceService = photoSourceService;
        _backgroundTaskManager = backgroundTaskManager;
    }
    
    public async Task ProcessAsync(long userId, long sourceId, PhotoSourceProcessingCommands command)
    {
        var taskName = $"UserId={userId}-SourceId={sourceId}";
        
        if (command == PhotoSourceProcessingCommands.Start)
        {
            var (downloadService, token) = await GetDownloadServiceAndTokenAsync(userId, sourceId);

            var cancellationTokenSource = new CancellationTokenSource();
            
            _backgroundTaskManager.Run(taskName, () => DoWork(downloadService, userId, token, cancellationTokenSource.Token), cancellationTokenSource);
        }
        else if (command == PhotoSourceProcessingCommands.Stop)
        {
            _backgroundTaskManager.Cancel(taskName);
            // take job and terminate it
        }
        else
        {
            throw new Exception("Unsupported command.");
        }
    }

    private static async Task DoWork(IDownloadService downloadService, long userId, string token, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var downloadedFileInfo in downloadService.DownloadAsync(userId, token, new StopDownloadAction(), cancellationToken))
            {
                // send file to photo processing service
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private async Task<(IDownloadService, string)> GetDownloadServiceAndTokenAsync(long userId, long sourceId)
    {
        try
        {
            var authSettings = await _userPhotoSourceService.GetUserAuthSettingsAsync(userId, sourceId);
            if (authSettings == null || authSettings.Token == null || authSettings.TokenExpiresOn > DateTimeOffset.UtcNow)
            {
                throw new Exception("User is not authorized.");
            }

            var photoSource = await _photoSourceService.GetByIdAsync(sourceId);

            return (_downloadServiceFactory.GetService(photoSource), authSettings.Token);
        }
        catch (NotFoundException e)
        {
            throw;
        }
        catch (Exception e)
        {
            throw;
        }
    }
}