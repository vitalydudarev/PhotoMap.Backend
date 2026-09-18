using Microsoft.Extensions.DependencyInjection;
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
    private readonly IPhotoSourceDownloadServiceFactory _downloadServiceFactory;
    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly IPhotoSourceService _photoSourceService;
    private readonly IFrontendNotificationService _frontendNotificationService;
    private readonly IBackgroundTaskManager _backgroundTaskManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly PhotoProcessingSettings _photoProcessingSettings;
    private readonly IFileStorage _fileStorage;

    public PhotoSourceProcessingService(
        IPhotoSourceDownloadServiceFactory downloadServiceFactory,
        IUserPhotoSourceService userPhotoSourceService,
        IPhotoSourceService photoSourceService,
        IFrontendNotificationService frontendNotificationService,
        IBackgroundTaskManager backgroundTaskManager,
        IServiceProvider serviceProvider,
        IOptions<PhotoProcessingSettings> photoProcessingSettings,
        IFileStorage fileStorage)
    {
        _downloadServiceFactory = downloadServiceFactory;
        _userPhotoSourceService = userPhotoSourceService;
        _photoSourceService = photoSourceService;
        _frontendNotificationService = frontendNotificationService;
        _backgroundTaskManager = backgroundTaskManager;
        _serviceProvider = serviceProvider;
        _photoProcessingSettings = photoProcessingSettings.Value;
        _fileStorage = fileStorage;
    }
    
    public async Task RunCommandAsync(long userId, long sourceId, PhotoSourceProcessingCommands command)
    {
        if (command == PhotoSourceProcessingCommands.Start)
        {
            await StartAsync(userId, sourceId);
        }
        else if (command == PhotoSourceProcessingCommands.Stop)
        {
            var taskName = GetTaskName(userId, sourceId);
            
            _backgroundTaskManager.CancelTask(taskName);
            // take job and terminate it
        }
        else
        {
            throw new Exception("Unsupported command.");
        }
    }

    private async Task StartAsync(long userId, long sourceId)
    {
        var taskName = GetTaskName(userId, sourceId);
        
        var token = await GetAuthTokenAsync(userId, sourceId);
        var photoSource = await _photoSourceService.GetByIdAsync(sourceId);
        var downloadService = CreateDownloadService(photoSource, userId, sourceId, token);

        // var totalFileCount = await downloadService.GetTotalFileCountAsync();

        var cancellationTokenSource = new CancellationTokenSource();

        _backgroundTaskManager.AddTask(taskName,
            () => DoWork(downloadService, _serviceProvider, _photoProcessingSettings.Sizes, userId, sourceId,
                photoSource.Name, cancellationTokenSource.Token), cancellationTokenSource);
    }

    private static async Task DoWork(
        IDownloadService downloadService,
        IServiceProvider serviceProvider,
        int[] sizes,
        // IFrontendNotificationService frontendNotificationService,
        long userId,
        long sourceId,
        string photoSourceName,
        CancellationToken cancellationToken)
    {
        var requestQueue = serviceProvider.GetRequiredService<IMessageQueue<ProcessImageRequest>>();
        var fileStorage = serviceProvider.GetRequiredService<IFileStorage>();
        
        try
        {
            await foreach (var downloadedFile in downloadService.DownloadAsync(cancellationToken))
            {
                // var name = downloadedFileInfo.ResourceName;
                // downloadedFileInfo.FileContents = [];

                var fileName = await fileStorage.SaveAsync($"Bin/{downloadedFile.FileInfo.ResourceName}", downloadedFile.FileContents);

                var request = new ProcessImageRequest
                {
                    DownloadedFileInfo = downloadedFile.FileInfo,
                    FileName = fileName,
                    Sizes = sizes,
                    UserId = userId,
                    PhotoSourceId = sourceId,
                    PhotoSourceName = photoSourceName
                };

                await requestQueue.EnqueueAsync(request, cancellationToken);

                // await frontendNotificationService.SendProgressAsync(userId, 111, 49, 33);
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
            await downloadService.DisposeAsync();
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

    private static string GetTaskName(long userId, long sourceId)
    {
        return $"UserId={userId}-SourceId={sourceId}";
    }

    private IDownloadService CreateDownloadService(PhotoSource photoSource, long userId, long sourceId, string token)
    {
        var parameters = new DownloadServiceParameters
        {
            UserId = userId,
            SourceId = sourceId,
            Token = token
        };

        return _downloadServiceFactory.GetService(photoSource, parameters);
    }
}