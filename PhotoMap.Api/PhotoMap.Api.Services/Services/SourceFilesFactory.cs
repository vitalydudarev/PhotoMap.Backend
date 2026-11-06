using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Shared.Yandex.Disk;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The files of the folder a photo source has its photos processed from, for the sources whose files are downloaded.
/// </summary>
public interface ISourceFilesFactory
{
    /// <returns>Whether the files of the photo source can be downloaded.</returns>
    bool Supports(PhotoSource photoSource);

    /// <returns>Whether the files of a folder of the photo source the user chooses can be downloaded.</returns>
    bool SupportsChosenFolders(PhotoSource photoSource);

    /// <exception cref="InvalidOperationException">The files of the photo source cannot be downloaded.</exception>
    ISourceFiles Create(PhotoSource photoSource, UserAuthResult authResult);
}

public class SourceFilesFactory : ISourceFilesFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YandexDiskSourceFiles> _logger;
    private readonly ILogger<DropboxSourceFiles> _dropboxLogger;

    public SourceFilesFactory(IHttpClientFactory httpClientFactory, ILogger<YandexDiskSourceFiles> logger,
        ILogger<DropboxSourceFiles> dropboxLogger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _dropboxLogger = dropboxLogger;
    }

    public bool Supports(PhotoSource photoSource)
    {
        return IsYandexDisk(photoSource) || IsDropbox(photoSource);
    }

    public bool SupportsChosenFolders(PhotoSource photoSource)
    {
        return IsYandexDisk(photoSource);
    }

    public ISourceFiles Create(PhotoSource photoSource, UserAuthResult authResult)
    {
        if (IsYandexDisk(photoSource))
        {
            var settings = new YandexDiskSettings();

            // Yandex.Disk tokens are not refreshed, the user authorizes again when the token has expired
            var apiClient = new ApiClient(authResult.Token, _httpClientFactory.CreateClient("yandexDiskClient"))
            {
                OpenDownloadTimeout = YandexDiskSourceFiles.DownloadTimeout
            };

            return new YandexDiskSourceFiles(apiClient, settings, _logger);
        }

        if (IsDropbox(photoSource))
        {
            var settings = new DropboxSettings();

            var appKey = photoSource.ClientAuthSettings.OAuthConfiguration.ClientId;
            var dropboxClient = DropboxClientFactory.Create(authResult, appKey, _httpClientFactory.CreateClient("dropboxClient"));

            // the downloads of the files are retried by DropboxSourceFiles alone, as many times as it says
            var downloadClient = DropboxClientFactory.Create(authResult, appKey, _httpClientFactory.CreateClient("dropboxClient"),
                maxRetriesOnError: 0);

            return new DropboxSourceFiles(dropboxClient, downloadClient, settings, _dropboxLogger);
        }

        throw new InvalidOperationException($"The files of photo source {photoSource.Name} cannot be downloaded.");
    }

    private static bool IsYandexDisk(PhotoSource photoSource)
    {
        return Type.GetType(photoSource.ServiceFactoryImplementationType) == typeof(YandexDiskDownloadServiceFactory);
    }

    private static bool IsDropbox(PhotoSource photoSource)
    {
        return Type.GetType(photoSource.ServiceFactoryImplementationType) == typeof(DropboxDownloadServiceFactory);
    }
}
