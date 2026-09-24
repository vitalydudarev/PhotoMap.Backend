using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services;

public class PhotoSourceDataService : IPhotoSourceDataService
{
    private readonly IPhotoService _photoService;
    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly IFailedFileService _failedFileService;
    private readonly IPhotoSourceProcessingService _photoSourceProcessingService;
    private readonly IFileStorage _fileStorage;
    private readonly IFrontendNotificationService _frontendNotificationService;
    private readonly ILogger<PhotoSourceDataService> _logger;

    public PhotoSourceDataService(
        IPhotoService photoService,
        IUserPhotoSourceService userPhotoSourceService,
        IFailedFileService failedFileService,
        IPhotoSourceProcessingService photoSourceProcessingService,
        IFileStorage fileStorage,
        IFrontendNotificationService frontendNotificationService,
        ILogger<PhotoSourceDataService> logger)
    {
        _photoService = photoService;
        _userPhotoSourceService = userPhotoSourceService;
        _failedFileService = failedFileService;
        _photoSourceProcessingService = photoSourceProcessingService;
        _fileStorage = fileStorage;
        _frontendNotificationService = frontendNotificationService;
        _logger = logger;
    }

    /// <summary>
    /// Deletes the photos of a photo source and what the runs that downloaded them left behind: the thumbnails,
    /// the status of the source, the state a stopped run resumes from and the files that failed. The photos
    /// themselves stay in the photo source, and the user stays authorized in it, so processing it again downloads
    /// them all anew.
    /// </summary>
    public async Task<bool> DeleteDataAsync(long userId, long sourceId)
    {
        // a run saves photos and its state while it goes, deleting either underneath it would only lose parts
        if (_photoSourceProcessingService.IsRunning(userId, sourceId))
        {
            return false;
        }

        var thumbnailPaths = await _photoService.DeleteByPhotoSourceAsync(userId, sourceId);

        foreach (var thumbnailPath in thumbnailPaths)
        {
            DeleteThumbnail(thumbnailPath);
        }

        await _userPhotoSourceService.DeleteUserPhotoStatusAsync(userId, sourceId);
        await _userPhotoSourceService.UpdateUserPhotoStateAsync(userId, sourceId, null);
        await _failedFileService.DeleteByPhotoSourceAsync(userId, sourceId);

        _logger.LogInformation("Deleted the data of photo source {SourceId} of user {UserId}", sourceId, userId);

        await NotifyDataDeletedAsync(userId, sourceId);

        return true;
    }

    /// <summary>
    /// The source starts over, which the pages watching it are told so that they do not keep reporting the
    /// counters of runs whose photos are gone.
    /// </summary>
    private Task NotifyDataDeletedAsync(long userId, long sourceId)
    {
        return _frontendNotificationService.SendProgressAsync(new UserPhotoSourceStatus
        {
            UserId = userId,
            PhotoSourceId = sourceId,
            Status = PhotoSourceStatus.NotStarted,
            LastUpdatedAt = DateTimeOffset.UtcNow
        });
    }

    private void DeleteThumbnail(string thumbnailPath)
    {
        try
        {
            _fileStorage.Delete(thumbnailPath);
        }
        catch (Exception e)
        {
            // the photo is gone from the database either way, a thumbnail left behind is not worth failing over
            _logger.LogWarning(e, "Failed to delete the thumbnail {ThumbnailPath}", thumbnailPath);
        }
    }
}
