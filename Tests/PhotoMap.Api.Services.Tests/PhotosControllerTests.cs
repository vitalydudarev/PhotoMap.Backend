using Microsoft.AspNetCore.Mvc;
using Moq;
using PhotoMap.Api.Controllers;
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

    private static Photo CreatePhoto(string? exifString)
    {
        return new Photo { Id = PhotoId, UserId = 1, FileName = "a.jpg", ExifString = exifString };
    }

    private PhotosController CreateController()
    {
        return new PhotosController(_photoProvider.Object, _photoService.Object);
    }
}
