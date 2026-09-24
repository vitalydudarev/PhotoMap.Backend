using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Implementations;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class PhotoProviderTests
{
    private const long PhotoId = 10;
    private const long UserId = 1;
    private const long PhotoSourceId = 2;

    private static readonly byte[] FileContents = [1, 2, 3];

    [Fact]
    public async Task GetPhotoAsync_ShouldDownloadFileFromPhotoSource()
    {
        // Arrange
        var photo = CreatePhoto(externalId: "id:abc123", path: "/Camera Uploads/photo.png");
        var photoSource = CreatePhotoSource();
        var downloadService = CreateDownloadService();
        var downloadServiceFactory = new Mock<IPhotoSourceDownloadServiceFactory>();
        downloadServiceFactory
            .Setup(a => a.GetService(photoSource, It.IsAny<DownloadServiceParameters>()))
            .Returns(downloadService.Object);

        var photoProvider = CreatePhotoProvider(photo, photoSource, CreateAuthResult(), downloadServiceFactory.Object);

        // Act
        var photoFile = await photoProvider.GetPhotoAsync(PhotoId, CancellationToken.None);

        // Assert
        Assert.NotNull(photoFile);
        Assert.Equal(FileContents, photoFile.Contents);
        Assert.Equal("photo.png", photoFile.FileName);
        Assert.Equal("image/png", photoFile.ContentType);

        downloadService.Verify(a => a.DownloadFileAsync(photo.ExternalId, photo.Path, It.IsAny<CancellationToken>()));
        downloadServiceFactory.Verify(a => a.GetService(photoSource,
            It.Is<DownloadServiceParameters>(b => b.UserId == UserId && b.SourceId == PhotoSourceId)));
    }

    [Fact]
    public async Task GetPhotoAsync_ShouldPassPath_WhenPhotoHasNoExternalId()
    {
        // Arrange
        var photo = CreatePhoto(externalId: null, path: "/Camera Uploads/photo.png");
        var downloadService = CreateDownloadService();
        var downloadServiceFactory = new Mock<IPhotoSourceDownloadServiceFactory>();
        downloadServiceFactory
            .Setup(a => a.GetService(It.IsAny<PhotoSource>(), It.IsAny<DownloadServiceParameters>()))
            .Returns(downloadService.Object);

        var photoProvider = CreatePhotoProvider(photo, CreatePhotoSource(), CreateAuthResult(), downloadServiceFactory.Object);

        // Act
        await photoProvider.GetPhotoAsync(PhotoId, CancellationToken.None);

        // Assert
        downloadService.Verify(a => a.DownloadFileAsync(null, photo.Path, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetPhotoAsync_ShouldReturnNull_WhenPhotoNotFound()
    {
        // Arrange
        var photoProvider = CreatePhotoProvider(null, CreatePhotoSource(), CreateAuthResult(),
            new Mock<IPhotoSourceDownloadServiceFactory>().Object);

        // Act
        var photoFile = await photoProvider.GetPhotoAsync(PhotoId, CancellationToken.None);

        // Assert
        Assert.Null(photoFile);
    }

    [Fact]
    public async Task GetPhotoAsync_ShouldThrow_WhenUserIsNotAuthorized()
    {
        // Arrange
        var expiredAuthResult = new UserAuthResult { Token = "token", TokenExpiresOn = DateTimeOffset.UtcNow.AddHours(-1) };
        var photoProvider = CreatePhotoProvider(CreatePhoto("id:abc123", "/photo.png"), CreatePhotoSource(),
            expiredAuthResult, new Mock<IPhotoSourceDownloadServiceFactory>().Object);

        // Act, Assert
        await Assert.ThrowsAsync<NotAuthorizedException>(
            () => photoProvider.GetPhotoAsync(PhotoId, CancellationToken.None));
    }

    [Fact]
    public async Task GetPhotoAsync_ShouldThrow_WhenPhotoHasNoFileReference()
    {
        // Arrange
        var photoProvider = CreatePhotoProvider(CreatePhoto(externalId: null, path: null), CreatePhotoSource(),
            CreateAuthResult(), new Mock<IPhotoSourceDownloadServiceFactory>().Object);

        // Act, Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => photoProvider.GetPhotoAsync(PhotoId, CancellationToken.None));
    }

    private static PhotoProvider CreatePhotoProvider(
        Photo? photo,
        PhotoSource photoSource,
        UserAuthResult authResult,
        IPhotoSourceDownloadServiceFactory downloadServiceFactory)
    {
        var photoService = new Mock<IPhotoService>();
        photoService.Setup(a => a.GetAsync(PhotoId)).ReturnsAsync(photo);

        var photoSourceService = new Mock<IPhotoSourceService>();
        photoSourceService.Setup(a => a.GetByIdAsync(PhotoSourceId)).ReturnsAsync(photoSource);

        var userPhotoSourceService = new Mock<IUserPhotoSourceService>();
        userPhotoSourceService.Setup(a => a.GetAuthResultAsync(UserId, PhotoSourceId)).ReturnsAsync(authResult);

        return new PhotoProvider(photoService.Object, photoSourceService.Object, userPhotoSourceService.Object,
            downloadServiceFactory, new Mock<IFileStorage>().Object);
    }

    private static Mock<IDownloadService> CreateDownloadService()
    {
        var downloadService = new Mock<IDownloadService>();
        downloadService
            .Setup(a => a.DownloadFileAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FileContents);

        return downloadService;
    }

    private static Photo CreatePhoto(string? externalId, string? path)
    {
        return new Photo
        {
            Id = PhotoId,
            UserId = UserId,
            PhotoSourceId = PhotoSourceId,
            FileName = "photo.png",
            ExternalId = externalId,
            Path = path,
            AddedOn = DateTimeOffset.UtcNow
        };
    }

    private static PhotoSource CreatePhotoSource()
    {
        return new PhotoSource
        {
            Id = PhotoSourceId,
            Name = "Dropbox",
            ServiceSettings = "Settings",
            ClientAuthSettings = new ClientAuthSettings
            {
                OAuthConfiguration = new OAuthConfiguration
                {
                    ClientId = "client-id",
                    RedirectUri = "",
                    ResponseType = "",
                    AuthorizeUrl = ""
                },
                RelativeAuthUrl = "/"
            },
            ServiceFactoryImplementationType = "Factory"
        };
    }

    private static UserAuthResult CreateAuthResult()
    {
        return new UserAuthResult { Token = "token", TokenExpiresOn = DateTimeOffset.UtcNow.AddHours(1) };
    }
}
