using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Tests.Stubs;

namespace PhotoMap.Api.Services.Tests;

public class PhotoSourceDownloadServiceFactoryTests
{
    [Fact]
    public void GetService_ShouldReturnServiceInstance()
    {
        // Arrange
        const int photoSourceId = 1;
        var photoSource = CreatePhotoSource(photoSourceId, typeof(DownloadServiceFactoryStub));

        var serviceFactories = new List<IDownloadServiceFactory> { new DownloadServiceFactoryStub() };

        var downloadServiceFactory = new PhotoSourceDownloadServiceFactory(serviceFactories);

        // Act
        var downloadService = downloadServiceFactory.GetService(photoSource);
        
        // Assert
        Assert.NotNull(downloadService);
        Assert.IsType<DownloadServiceStub>(downloadService);
    }

    private static PhotoSource CreatePhotoSource(long id, Type serviceFactoryType)
    {
        return new PhotoSource
        {
            Id = id,
            Name = "Test",
            ServiceSettings = "Settings",
            ClientAuthSettings = new ClientAuthSettings
            {
                OAuthConfiguration = new OAuthConfiguration
                {
                    ClientId = "",
                    RedirectUri = "",
                    ResponseType = "",
                    AuthorizeUrl = ""
                },
                RelativeAuthUrl = "/"
            },
            ServiceFactoryImplementationType = TypeHelper.GetTypeFullName(serviceFactoryType)
        };
    }
}
