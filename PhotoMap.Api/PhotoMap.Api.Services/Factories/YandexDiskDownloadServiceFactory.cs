using System.Text.Json;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Factories;

/// <summary>
/// Resolved from the service scope of a processing run, the created service uses the services of that scope.
/// </summary>
public class YandexDiskDownloadServiceFactory : IDownloadServiceFactory
{
    private readonly ILogger<YandexDiskDownloadService> _logger;
    private readonly IYandexDiskDownloadStateService _downloadStateService;
    private readonly IPhotoService _photoService;
    private readonly IFailedFileService _failedFileService;
    private readonly IHttpClientFactory _httpClientFactory;

    public YandexDiskDownloadServiceFactory(
        ILogger<YandexDiskDownloadService> logger,
        IYandexDiskDownloadStateService downloadStateService,
        IPhotoService photoService,
        IFailedFileService failedFileService,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _downloadStateService = downloadStateService;
        _photoService = photoService;
        _failedFileService = failedFileService;
        _httpClientFactory = httpClientFactory;
    }

    public IDownloadService Create(string settingsSerialized, DownloadServiceParameters parameters)
    {
        var settings = JsonSerializer.Deserialize<YandexDiskSettings>(settingsSerialized);
        if (settings == null)
        {
            throw new Exception("Unable to deserialize settings");
        }

        return new YandexDiskDownloadService(_logger, _downloadStateService, _photoService, _failedFileService,
            _httpClientFactory, settings, parameters);
    }
}
