using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Factories;

public class DropboxDownloadServiceFactory : IDownloadServiceFactory
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    
    public DropboxDownloadServiceFactory(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }
    
    public IDownloadService Create(string settingsSerialized, DownloadServiceParameters parameters)
    {
        var settings = JsonSerializer.Deserialize<DropboxSettings>(settingsSerialized);
        if (settings == null)
        {
            throw new Exception("Unable to deserialize settings");
        }

        var serviceProvider = _serviceScopeFactory.CreateScope().ServiceProvider;
        
        var logger = serviceProvider.GetRequiredService<ILogger<DropboxDownloadService>>();
        var downloadStateService = serviceProvider.GetRequiredService<IDropboxDownloadStateService>();
        var progressReporter = serviceProvider.GetRequiredService<IProgressReporter>();

        return new DropboxDownloadService(logger, downloadStateService, progressReporter, settings, parameters);
    }
}