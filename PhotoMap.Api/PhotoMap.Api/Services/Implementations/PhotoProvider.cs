using System;
using System.IO;
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
    private readonly ISourceFilesFactory _sourceFilesFactory;

    public PhotoProvider(
        IPhotoService photoService,
        IPhotoSourceService photoSourceService,
        IUserPhotoSourceService userPhotoSourceService,
        IPhotoSourceDownloadServiceFactory downloadServiceFactory,
        IFileStorage fileStorage,
        IImageConverter imageConverter,
        ISourceFilesFactory sourceFilesFactory)
    {
        _photoService = photoService;
        _photoSourceService = photoSourceService;
        _userPhotoSourceService = userPhotoSourceService;
        _downloadServiceFactory = downloadServiceFactory;
        _fileStorage = fileStorage;
        _imageConverter = imageConverter;
        _sourceFilesFactory = sourceFilesFactory;
    }

    /// <summary>
    /// Only thumbnails are stored by the application, the photo itself is downloaded from the source it came from,
    /// with the credentials of the user it belongs to. When the user is not authorized in that source, the photo is
    /// downloaded as one of its copies, from the first source of theirs the user is authorized in. A photo in a format
    /// not all browsers can show is served converted to JPEG.
    /// </summary>
    /// <exception cref="NotAuthorizedException">The user is authorized in none of the sources of the photo and its
    /// copies.</exception>
    public async Task<PhotoFile?> GetPhotoAsync(long id, CancellationToken cancellationToken)
    {
        var photo = await _photoService.GetAsync(id);
        if (photo == null)
        {
            return null;
        }

        var (file, photoSource, authResult) = await FindAuthorizedFileAsync(photo);

        return await DownloadPhotoAsync(photo, file, photoSource, authResult, cancellationToken);
    }

    /// <summary>
    /// The photo to be viewed: the preview its source makes of it for viewing, much smaller than the photo, the XXXL
    /// preview on Yandex.Disk. The photo itself, as <see cref="GetPhotoAsync"/> gives it, when the source makes no
    /// such preview or has not made it of the photo.
    /// </summary>
    /// <exception cref="NotAuthorizedException">The user is authorized in none of the sources of the photo and its
    /// copies.</exception>
    public async Task<PhotoFile?> GetPreviewAsync(long id, CancellationToken cancellationToken)
    {
        var photo = await _photoService.GetAsync(id);
        if (photo == null)
        {
            return null;
        }

        var (file, photoSource, authResult) = await FindAuthorizedFileAsync(photo);

        // Yandex.Disk finds files by their path, Dropbox by either
        var reference = file.Path ?? file.ExternalId;

        if (!string.IsNullOrEmpty(reference) && _sourceFilesFactory.Supports(photoSource))
        {
            using var sourceFiles = _sourceFilesFactory.Create(photoSource, authResult);

            var preview = await sourceFiles.DownloadViewingPreviewAsync(reference, cancellationToken);
            if (preview != null)
            {
                return new PhotoFile(preview.Contents, FileNameOf(photo, preview.ContentType), preview.ContentType);
            }
        }

        return await DownloadPhotoAsync(photo, file, photoSource, authResult, cancellationToken);
    }

    /// <param name="photo">The photo asked for.</param>
    /// <param name="file">The photo or the copy of it that is downloaded.</param>
    private async Task<PhotoFile> DownloadPhotoAsync(Photo photo, Photo file, PhotoSource photoSource,
        UserAuthResult authResult, CancellationToken cancellationToken)
    {
        // photos saved before the sources started reporting file IDs are only identified by their path
        if (string.IsNullOrEmpty(file.ExternalId) && string.IsNullOrEmpty(file.Path))
        {
            throw new NotFoundException($"Photo with ID {file.Id} has no reference to the file in its photo source.");
        }

        await using var downloadService = _downloadServiceFactory.GetService(photoSource,
            CreateDownloadServiceParameters(file, photoSource, authResult));

        var fileContents = await downloadService.DownloadFileAsync(file.ExternalId, file.Path, cancellationToken);

        if (_imageConverter.NeedsConversion(file.FileName))
        {
            return new PhotoFile(_imageConverter.ConvertToJpeg(fileContents), FileNameOf(photo, "image/jpeg"), "image/jpeg");
        }

        return new PhotoFile(fileContents, photo.FileName, SupportedImageFormats.GetContentType(file.FileName));
    }

    /// <summary>
    /// The file name of the photo, with the extension of a JPEG when it is served as one but is not: a HEIC photo
    /// converted, or the preview of it.
    /// </summary>
    private static string FileNameOf(Photo photo, string contentType)
    {
        var extension = Path.GetExtension(photo.FileName);
        var isJpeg = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

        return contentType == "image/jpeg" && !isJpeg ? Path.ChangeExtension(photo.FileName, ".jpg") : photo.FileName;
    }

        /// <returns>Null when the photo or its thumbnail is not found, also when the thumbnail file is missing.</returns>
    public async Task<byte[]?> GetThumbAsync(long id, string size)
    {
        var photo = await _photoService.GetAsync(id);
        if (photo != null)
        {
            var filePath = size == "small" ? photo.ThumbnailSmallFilePath : photo.ThumbnailLargeFilePath;
            if (filePath != null)
            {
                try
                {
                    return await _fileStorage.GetAsync(filePath);
                }
                catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
                {
                    return null;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The photo, or the first of its copies, in a source the user is authorized in, the photo itself first.
    /// </summary>
    private async Task<(Photo File, PhotoSource Source, UserAuthResult AuthResult)> FindAuthorizedFileAsync(Photo photo)
    {
        var photoSource = await _photoSourceService.GetByIdAsync(photo.PhotoSourceId);
        var authResult = await _userPhotoSourceService.GetAuthResultAsync(photo.UserId, photoSource.Id);

        if (authResult is { IsValid: true })
        {
            return (photo, photoSource, authResult);
        }

        // the copies in the same source are of no help, its credentials are the ones missing
        foreach (var copy in await _photoService.GetCopiesAsync(photo))
        {
            if (copy.PhotoSourceId == photo.PhotoSourceId)
            {
                continue;
            }

            var copyAuthResult = await _userPhotoSourceService.GetAuthResultAsync(copy.UserId, copy.PhotoSourceId);

            if (copyAuthResult is { IsValid: true })
            {
                return (copy, await _photoSourceService.GetByIdAsync(copy.PhotoSourceId), copyAuthResult);
            }
        }

        throw new NotAuthorizedException($"User is not authorized in photo source {photoSource.Name}.");
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
