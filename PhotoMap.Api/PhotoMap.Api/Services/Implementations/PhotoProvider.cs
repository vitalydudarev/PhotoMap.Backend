using System.Threading;
using System.Threading.Tasks;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Interfaces;
using PhotoMap.Api.Services.Services;
using PhotoMap.Worker.Services.Definitions;

namespace PhotoMap.Api.Services.Implementations;

public class PhotoProvider : IPhotoProvider
{
    private readonly IPhotoService _photoService;
    private readonly IPhotoSourceService _photoSourceService;
    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly IPhotoSourceDownloadServiceFactory _downloadServiceFactory;
    private readonly IFileStorage _fileStorage;
    private readonly IImageConverter _imageConverter;

    public PhotoProvider(
        IPhotoService photoService,
        IPhotoSourceService photoSourceService,
        IUserPhotoSourceService userPhotoSourceService,
        IPhotoSourceDownloadServiceFactory downloadServiceFactory,
        IFileStorage fileStorage,
        IImageConverter imageConverter)
    {
        _photoService = photoService;
        _photoSourceService = photoSourceService;
        _userPhotoSourceService = userPhotoSourceService;
        _downloadServiceFactory = downloadServiceFactory;
        _fileStorage = fileStorage;
        _imageConverter = imageConverter;
    }

    /// <summary>
    /// Only thumbnails are stored by the application, the photo itself is downloaded from the source it came from,
    /// with the credentials of the user it belongs to. A photo in a format not all browsers can show is served
    /// converted to JPEG.
    /// </summary>
    public async Task<PhotoFile?> GetPhotoAsync(long id, CancellationToken cancellationToken)
    {
        var photo = await _photoService.GetAsync(id);
        if (photo == null)
        {
            return null;
        }

        // photos saved before the sources started reporting file IDs are only identified by their path
        if (string.IsNullOrEmpty(photo.ExternalId) && string.IsNullOrEmpty(photo.Path))
        {
            throw new NotFoundException($"Photo with ID {id} has no reference to the file in its photo source.");
        }

        var photoSource = await _photoSourceService.GetByIdAsync(photo.PhotoSourceId);
        var authResult = await GetAuthResultAsync(photo.UserId, photoSource);

        await using var downloadService = _downloadServiceFactory.GetService(photoSource,
            CreateDownloadServiceParameters(photo, photoSource, authResult));

        var fileContents = await downloadService.DownloadFileAsync(photo.ExternalId, photo.Path, cancellationToken);

        if (_imageConverter.NeedsConversion(photo.FileName))
        {
            return new PhotoFile(_imageConverter.ConvertToJpeg(fileContents), photo.FileName, "image/jpeg");
        }

        return new PhotoFile(fileContents, photo.FileName, SupportedImageFormats.GetContentType(photo.FileName));
    }
        
    public async Task<byte[]?> GetThumbAsync(long id, string size)
    {
        var photo = await _photoService.GetAsync(id);
        if (photo != null)
        {
            var filePath = size == "small" ? photo.ThumbnailSmallFilePath : photo.ThumbnailLargeFilePath;
            if (filePath != null)
            {
                return await _fileStorage.GetAsync(filePath);
            }
        }

        return null;
    }

    private async Task<UserAuthResult> GetAuthResultAsync(long userId, PhotoSource photoSource)
    {
        var authResult = await _userPhotoSourceService.GetAuthResultAsync(userId, photoSource.Id);
        if (authResult == null || !authResult.IsValid)
        {
            throw new NotAuthorizedException($"User is not authorized in photo source {photoSource.Name}.");
        }

        return authResult;
    }

    private static DownloadServiceParameters CreateDownloadServiceParameters(Photo photo, PhotoSource photoSource,
        UserAuthResult authResult)
    {
        return new DownloadServiceParameters
        {
            UserId = photo.UserId,
            SourceId = photoSource.Id,
            AuthResult = authResult,
            ClientId = photoSource.ClientAuthSettings.OAuthConfiguration.ClientId,
            // the counters belong to a processing run, downloading a single file does not report progress
            Progress = new ProcessingProgress(0, 0)
        };
    }
}
