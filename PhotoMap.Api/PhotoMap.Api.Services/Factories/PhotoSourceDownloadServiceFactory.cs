using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Factories;

public class PhotoSourceDownloadServiceFactory : IPhotoSourceDownloadServiceFactory
{
    private readonly IEnumerable<IDownloadServiceFactory> _serviceFactories;

    public PhotoSourceDownloadServiceFactory(IEnumerable<IDownloadServiceFactory> serviceFactories)
    {
        _serviceFactories = serviceFactories;
    }

    public IDownloadService GetService(PhotoSource photoSource)
    {
        var type = Type.GetType(photoSource.ServiceFactoryImplementationType);
        if (type != null)
        {
            var serviceFactory = _serviceFactories.FirstOrDefault(a => a.GetType() == type);
            if (serviceFactory != null)
            {
                return serviceFactory.Create(photoSource.ServiceSettings);
            }
        }

        throw new Exception($"Unable to resolve service factory for source {photoSource.Name}");
    }
}