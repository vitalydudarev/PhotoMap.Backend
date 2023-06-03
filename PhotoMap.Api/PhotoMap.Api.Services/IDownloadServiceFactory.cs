using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

public interface IDownloadServiceFactory
{
    IDownloadService Create(string settingsSerialized);
}