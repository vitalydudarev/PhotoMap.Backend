using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Factories;

public interface IDownloadServiceFactory
{
    IDownloadService Create(string settingsSerialized, DownloadServiceParameters parameters);
}