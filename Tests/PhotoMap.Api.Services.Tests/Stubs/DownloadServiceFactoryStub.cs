using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests.Stubs;

public class DownloadServiceFactoryStub : IDownloadServiceFactory
{
    public IDownloadService Create(string settingsSerialized, DownloadServiceParameters parameters)
    {
        return new DownloadServiceStub();
    }
}