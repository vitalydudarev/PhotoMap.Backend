using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;
using PhotoMap.Api.Controllers;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.DTOs;

namespace PhotoMap.Api.Services.Tests;

public class UsersControllerTests
{
    private const long UserId = 1;

    private readonly Mock<IPhotoService> _photoService = new();
    private readonly Mock<IUserService> _userService = new();

    public UsersControllerTests()
    {
        _photoService
            .Setup(a => a.GetByUserIdAsync(UserId, It.IsAny<PhotoFilter>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<PhotoSortOrder>()))
            .ReturnsAsync([]);
        _photoService
            .Setup(a => a.GetTotalCountByUserIdAsync(UserId, It.IsAny<PhotoFilter>()))
            .ReturnsAsync(7);
    }

    [Fact]
    public async Task GetUserPhotos_ShouldTakeThePhotosAndTheCountFromTheGivenSourcesAndYears()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetUserPhotos(UserId, 100, 0, PhotoSortOrder.Asc, [1, 3], [2016, 2019]);

        // Assert
        var response = Assert.IsType<PagedResponse<PhotoDto>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(7, response.Total);

        _photoService.Verify(a => a.GetByUserIdAsync(UserId,
            It.Is<PhotoFilter>(f => IsFilter(f, new long[] { 1, 3 }, new[] { 2016, 2019 })), 100, 0, PhotoSortOrder.Asc));
        _photoService.Verify(a => a.GetTotalCountByUserIdAsync(UserId,
            It.Is<PhotoFilter>(f => IsFilter(f, new long[] { 1, 3 }, new[] { 2016, 2019 }))));
    }

    [Fact]
    public async Task GetUserPhotos_ShouldTakeThePhotosFromAllSourcesAndYears_WhenNoneIsGiven()
    {
        // Arrange
        var controller = CreateController();

        // Act
        await controller.GetUserPhotos(UserId, 100, 0);

        // Assert
        _photoService.Verify(a => a.GetByUserIdAsync(UserId,
            It.Is<PhotoFilter>(f => IsFilter(f, Array.Empty<long>(), Array.Empty<int>())), 100, 0, PhotoSortOrder.Asc));
        _photoService.Verify(a => a.GetTotalCountByUserIdAsync(UserId,
            It.Is<PhotoFilter>(f => IsFilter(f, Array.Empty<long>(), Array.Empty<int>()))));
    }

    [Fact]
    public async Task GetUserPhotos_ShouldTakeThePhotosOfTheGivenCategories()
    {
        // Arrange
        var controller = CreateController();

        // Act
        await controller.GetUserPhotos(UserId, 100, 0, category: [PhotoCategory.Screenshot, PhotoCategory.Other]);

        // Assert
        _photoService.Verify(a => a.GetByUserIdAsync(UserId,
            It.Is<PhotoFilter>(f => f.Categories.SequenceEqual(new[] { PhotoCategory.Screenshot, PhotoCategory.Other })),
            100, 0, PhotoSortOrder.Asc));
        _photoService.Verify(a => a.GetTotalCountByUserIdAsync(UserId,
            It.Is<PhotoFilter>(f => f.Categories.SequenceEqual(new[] { PhotoCategory.Screenshot, PhotoCategory.Other }))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetUserPhotos_ShouldTakeThePhotosWithOrWithoutAGpsLocation(bool gps)
    {
        // Arrange
        var controller = CreateController();

        // Act
        await controller.GetUserPhotos(UserId, 100, 0, gps: gps);

        // Assert
        _photoService.Verify(a => a.GetByUserIdAsync(UserId, It.Is<PhotoFilter>(f => f.HasGps == gps), 100, 0,
            PhotoSortOrder.Asc));
        _photoService.Verify(a => a.GetTotalCountByUserIdAsync(UserId, It.Is<PhotoFilter>(f => f.HasGps == gps)));
    }

    [Fact]
    public async Task GetUserPhotos_ShouldReturnBadRequest_WhenTheCategoryIsNotOne()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetUserPhotos(UserId, 100, 0, category: [(PhotoCategory)42]);

        // Assert
        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsAssignableFrom<ObjectResult>(result).Value);
        Assert.Contains("category", problem.Errors.Keys);
        _photoService.Verify(a => a.GetByUserIdAsync(It.IsAny<long>(), It.IsAny<PhotoFilter>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<PhotoSortOrder>()), Times.Never);
    }

    [Fact]
    public async Task GetUserPhotos_ShouldTellWhenThePhotoWasMarkedAsDeleted()
    {
        // Arrange
        var deletedOn = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.FromHours(3));
        _photoService
            .Setup(a => a.GetByUserIdAsync(UserId, It.IsAny<PhotoFilter>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<PhotoSortOrder>()))
            .ReturnsAsync([new Photo { Id = 5, UserId = UserId, FileName = "a.jpg", DeletedOn = deletedOn }]);
        var controller = CreateController();

        // Act
        var result = await controller.GetUserPhotos(UserId, 100, 0, category: [PhotoCategory.Deleted]);

        // Assert
        var response = Assert.IsType<PagedResponse<PhotoDto>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(deletedOn.UtcDateTime, Assert.Single(response.Values).DeletedOn);
    }

    [Theory]
    [InlineData(true, StatusCodes.Status204NoContent)]
    [InlineData(false, StatusCodes.Status404NotFound)]
    public async Task MarkPhotoAsDeleted_ShouldMarkThePhoto_OrReturnNotFound(bool found, int statusCode)
    {
        // Arrange
        _photoService.Setup(a => a.MarkAsDeletedAsync(UserId, 5)).ReturnsAsync(found);
        var controller = CreateController();

        // Act
        var result = await controller.MarkPhotoAsDeleted(UserId, 5);

        // Assert
        Assert.Equal(statusCode, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(true, StatusCodes.Status204NoContent)]
    [InlineData(false, StatusCodes.Status404NotFound)]
    public async Task RestorePhoto_ShouldRestoreThePhoto_OrReturnNotFound(bool found, int statusCode)
    {
        // Arrange
        _photoService.Setup(a => a.RestoreAsync(UserId, 5)).ReturnsAsync(found);
        var controller = CreateController();

        // Act
        var result = await controller.RestorePhoto(UserId, 5);

        // Assert
        Assert.Equal(statusCode, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetUserPhotoYears_ShouldReturnTheYearsOfThePhotos()
    {
        // Arrange
        _photoService.Setup(a => a.GetYearsAsync(UserId)).ReturnsAsync([2016, 2019]);
        var controller = CreateController();

        // Act
        var result = await controller.GetUserPhotoYears(UserId);

        // Assert
        Assert.Equal([2016, 2019], Assert.IsAssignableFrom<IReadOnlyList<int>>(Assert.IsType<OkObjectResult>(result).Value));
    }

    private static bool IsFilter(PhotoFilter filter, long[] photoSourceIds, int[] years)
    {
        return filter.PhotoSourceIds.SequenceEqual(photoSourceIds) && filter.Years.SequenceEqual(years) &&
               filter.Categories.Count == 0 && filter.HasGps == null;
    }

    private UsersController CreateController()
    {
        var hostInfo = new HostInfo { Scheme = "https", Host = new HostString("localhost", 5001) };

        return new UsersController(_photoService.Object, _userService.Object, hostInfo);
    }
}
