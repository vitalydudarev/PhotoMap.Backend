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

    public async Task<IDownloadService?> Create(long id)
    {
        var photoSource = await _photoSourceService.GetByIdAsync(id);
        if (photoSource == null)
        {
            _logger.LogError("Photo source with ID {ID} does not exist", id);
            throw new Exception($"Photo source with ID {id} does not exist");
        }

        return null;
    }
    
    public async Task<IDownloadService?> GetService(long id)
    {
        var photoSources = (await _photoSourceService.GetAsync()).ToDictionary(a => a.Id, b => b);
        if (!photoSources.TryGetValue(id, out var photoSource))
        {
            _logger.LogError("Service with ID {Id} not found", id);
            throw new Exception($"Service with ID {id} not found");
        }

        var type = Type.GetType(photoSource.ServiceImplementationType);
        if (type != null)
        {
            var service = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService(type);

            return (IDownloadService) service;
        }
        
        /*
         * var cachedProvider = _memoryCache.Get<IQuoteProvider>(quoteSource.ImplementationClass);
        if (cachedProvider != null)
        {
            return cachedProvider;
        }

        var type = Type.GetType(quoteSource.ImplementationClass);
        if (type != null)
        {
            var parameters = new object[] {_serviceScopeFactory,  quoteSource.Url, quoteSource.Name};

            try
            {
                var instance = Activator.CreateInstance(type, parameters);
                if (instance != null)
                {
                    // TODO: all instances should be disposed at a later stage - implement it
                    _memoryCache.Set(quoteSource.ImplementationClass, instance);
                    return (IQuoteProvider)instance;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to create provider of {Type}", quoteSource.ImplementationClass);
                throw new Exception($"Unable to create provider of {quoteSource.ImplementationClass}");
            }
        }
         */

        return null;
    }
}