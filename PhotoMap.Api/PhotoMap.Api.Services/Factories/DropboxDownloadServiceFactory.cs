using System.Text.Json;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Factories;

/// <summary>
/// Resolved from the service scope of a processing run, the created service uses the services of that scope.
/// </summary>
public class DropboxDownloadServiceFactory : IDownloadServiceFactory
{
    private readonly ILogger<DropboxDownloadService> _logger;
    private readonly IDropboxDownloadStateService _downloadStateService;
    private readonly IProgressReporter _progressReporter;
    private readonly IPhotoService _photoService;
    private readonly IFailedFileService _failedFileService;
    private readonly IHttpClientFactory _httpClientFactory;

    public DropboxDownloadServiceFactory(
        ILogger<DropboxDownloadService> logger,
        IDropboxDownloadStateService downloadStateService,
        IProgressReporter progressReporter,
        IPhotoService photoService,
        IFailedFileService failedFileService,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _downloadStateService = downloadStateService;
        _progressReporter = progressReporter;
        _photoService = photoService;
        _failedFileService = failedFileService;
        _httpClientFactory = httpClientFactory;
    }
    
    public IDownloadService Create(string settingsSerialized, DownloadServiceParameters parameters)
    {
        var settings = JsonSerializer.Deserialize<DropboxSettings>(settingsSerialized);
        if (settings == null)
        {
            throw new Exception("Unable to deserialize settings");
        }

        return new DropboxDownloadService(_logger, _downloadStateService, _progressReporter, _photoService,
            _failedFileService, _httpClientFactory, settings, parameters);
    }
}
