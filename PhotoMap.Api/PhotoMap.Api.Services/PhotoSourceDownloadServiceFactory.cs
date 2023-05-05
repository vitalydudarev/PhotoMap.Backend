using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

public class PhotoSourceDownloadServiceFactory : IPhotoSourceDownloadServiceFactory
{
    private readonly ILogger<PhotoSourceDownloadServiceFactory> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IPhotoSourceService _photoSourceService;

    public PhotoSourceDownloadServiceFactory(
        ILogger<PhotoSourceDownloadServiceFactory> logger,
        IServiceScopeFactory serviceScopeFactory,
        IPhotoSourceService photoSourceService)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _photoSourceService = photoSourceService;
    }
    
    public async Task<IDownloadService?> GetService(long id)
    {
        var photoSources = (await _photoSourceService.GetAsync()).ToDictionary(a => a.Id, b => b);
        if (!photoSources.TryGetValue(id, out var photoSource))
        {
            _logger.LogError("Service with ID {Id} not found", id);
            throw new Exception($"Service with ID {id} not found");
        }

        var type = Type.GetType(photoSource.ImplementationType);
        if (type != null)
        {
            var service = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService(type);

            return (IDownloadService) service;
        }

        return null;
    }
}