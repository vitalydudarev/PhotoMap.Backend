using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PhotoMap.Api.Controllers;
using PhotoMap.Api.DTOs;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Interfaces;

namespace PhotoMap.Api.Services.Tests;

public class PhotosControllerTests
{
    private const long PhotoId = 5;

    private readonly Mock<IPhotoProvider> _photoProvider = new();
    private readonly Mock<IPhotoService> _photoService = new();

    [Fact]
    public async Task GetExifAsync_ShouldReturnTheExifAsItWasSaved()
    {
        // Arrange
        const string exif = "{\"ExifIfd0\":{\"Make\":\"DJI\"}}";
        _photoService.Setup(a => a.GetAsync(PhotoId)).ReturnsAsync(CreatePhoto(exif));
        var controller = CreateController();

        // Act
        var result = await controller.GetExifAsync(PhotoId);

        // Assert
        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(exif, content.Content);
        Assert.Equal("application/json", content.ContentType);
    }

    [Fact]
    public async Task GetExifAsync_ShouldReturnAnEmptyObject_WhenThePhotoHasNoExif()
    {
        // Arrange
        _photoService.Setup(a => a.GetAsync(PhotoId)).ReturnsAsync(CreatePhoto(null));
        var controller = CreateController();

        // Act
        var result = await controller.GetExifAsync(PhotoId);

        // Assert
        Assert.Equal("{}", Assert.IsType<ContentResult>(result).Content);
    }

    [Fact]
    public async Task GetExifAsync_ShouldReturnNotFound_WhenThereIsNoSuchPhoto()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetExifAsync(PhotoId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetAddressAsync_ShouldReturnTheGeocodedAddress()
    {
        // Arrange
        var photo = CreatePhoto(null);
        photo.Geocoding = "{\"type\":\"FeatureCollection\",\"features\":[" +
                          "{\"properties\":{\"feature_type\":\"street\",\"full_address\":\"Main Street\"}}," +
                          "{\"properties\":{\"feature_type\":\"address\",\"full_address\":\"12 Main Street\"}}]}";
        _photoService.Setup(a => a.GetAsync(PhotoId)).ReturnsAsync(photo);
        var controller = CreateController();

        // Act
        var result = await controller.GetAddressAsync(PhotoId);

        // Assert
        var dto = Assert.IsType<PhotoAddressDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("12 Main Street", dto.Address);
    }

    [Fact]
    public async Task GetAddressAsync_ShouldReturnNoAddress_WhenThePhotoIsNotGeocoded()
    {
        // Arrange
        _photoService.Setup(a => a.GetAsync(PhotoId)).ReturnsAsync(CreatePhoto(null));
        var controller = CreateController();

        // Act
        var result = await controller.GetAddressAsync(PhotoId);

        // Assert
        Assert.Null(Assert.IsType<PhotoAddressDto>(Assert.IsType<OkObjectResult>(result).Value).Address);
    }

    [Fact]
    public async Task GetAddressAsync_ShouldReturnNotFound_WhenThereIsNoSuchPhoto()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetAddressAsync(PhotoId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetPhotoAsync_ShouldShowThePhotoInline_UnderItsFileName()
    {
        // Arrange
        _photoProvider.Setup(a => a.GetPhotoAsync(PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoFile([1, 2, 3], "Отпуск 2024.jpg", "image/jpeg"));
        var controller = CreateController();

        // Act
        var result = await controller.GetPhotoAsync(PhotoId, CancellationToken.None);

        // Assert
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.Empty(file.FileDownloadName);

        var contentDisposition = controller.Response.Headers.ContentDisposition.ToString();
        Assert.StartsWith("inline;", contentDisposition);
        Assert.Contains("filename*=UTF-8''%D0%9E%D1%82%D0%BF%D1%83%D1%81%D0%BA%202024.jpg", contentDisposition);
    }

    [Fact]
    public async Task GetPhotoAsync_ShouldReturnNotFound_WhenThereIsNoSuchPhoto()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetPhotoAsync(PhotoId, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetPreviewAsync_ShouldPassThePreviewOn_ForTheBrowserToKeep()
    {
        // Arrange
        var contents = new MemoryStream([1, 2, 3]);
        _photoProvider.Setup(a => a.GetPreviewAsync(PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoStream(contents, "IMG_0001.jpg", "image/jpeg"));
        var controller = CreateController();

        // Act
        var result = await controller.GetPreviewAsync(PhotoId, CancellationToken.None);

        // Assert
        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Same(contents, file.FileStream);
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.Equal("private, max-age=604800", controller.Response.Headers.CacheControl.ToString());
        Assert.StartsWith("inline;", controller.Response.Headers.ContentDisposition.ToString());
    }

    [Fact]
    public async Task GetPreviewAsync_ShouldReturnNotFound_WhenThereIsNoSuchPhoto()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetPreviewAsync(PhotoId, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(controller.Response.Headers.CacheControl.ToString());
    }

    private static Photo CreatePhoto(string? exifString)
    {
        return new Photo { Id = PhotoId, UserId = 1, FileName = "a.jpg", ExifString = exifString };
    }

    private PhotosController CreateController()
    {
        return new PhotosController(_photoProvider.Object, _photoService.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
