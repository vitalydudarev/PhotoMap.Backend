using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

public interface IPhotoSourceDownloadServiceFactory
{
    Task<IDownloadService?> GetService(long id);
}