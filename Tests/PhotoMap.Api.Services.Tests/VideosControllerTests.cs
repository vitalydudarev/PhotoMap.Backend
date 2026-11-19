using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PhotoMap.Api.Controllers;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.DTOs;
using PhotoMap.Api.Services;
using PhotoMap.Api.Services.Interfaces;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class VideosControllerTests
{
    private const long UserId = 1;

    private readonly Mock<IVideoService> _videoService = new();

    public VideosControllerTests()
    {
        _videoService
            .Setup(a => a.GetByUserIdAsync(UserId, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<LocationFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<PhotoSortOrder>()))
            .ReturnsAsync([]);
        _videoService
            .Setup(a => a.GetTotalCountByUserIdAsync(UserId, It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<LocationFilter>()))
            .ReturnsAsync(7);
    }

    [Fact]
    public async Task GetUserVideos_ShouldTakeTheVideosAndTheCountFromTheGivenFoldersAndYears()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetUserVideos(UserId, 100, 0, PhotoSortOrder.Desc, ["disk:/Фотокамера"], [2016, 2019]);

        // Assert
        var response = Assert.IsType<PagedResponse<VideoDto>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(7, response.Total);

        _videoService.Verify(a => a.GetByUserIdAsync(UserId,
            It.Is<IReadOnlyCollection<string>>(f => f.SequenceEqual(new[] { "disk:/Фотокамера" })),
            It.Is<IReadOnlyCollection<int>>(y => y.SequenceEqual(new[] { 2016, 2019 })), It.IsAny<LocationFilter>(), 100,
            0, PhotoSortOrder.Desc));
        _videoService.Verify(a => a.GetTotalCountByUserIdAsync(UserId,
            It.Is<IReadOnlyCollection<string>>(f => f.SequenceEqual(new[] { "disk:/Фотокамера" })),
            It.Is<IReadOnlyCollection<int>>(y => y.SequenceEqual(new[] { 2016, 2019 })), It.IsAny<LocationFilter>()));
    }

    [Fact]
    public async Task GetUserVideos_ShouldTakeTheVideosOfAllFoldersAndYears_WhenNoneIsGiven()
    {
        // Arrange
        var controller = CreateController();

        // Act
        await controller.GetUserVideos(UserId, 100, 0);

        // Assert
        _videoService.Verify(a => a.GetByUserIdAsync(UserId, It.Is<IReadOnlyCollection<string>>(f => f.Count == 0),
            It.Is<IReadOnlyCollection<int>>(y => y.Count == 0), It.Is<LocationFilter>(l => !l.FiltersByLocations), 100,
            0, PhotoSortOrder.Asc));
    }

    [Fact]
    public async Task GetUserVideos_ShouldTakeTheVideosAndTheCountFromTheGivenLocations()
    {
        // Arrange
        var controller = CreateController();

        // Act
        await controller.GetUserVideos(UserId, 100, 0, location: [12, 40], unknownLocation: true);

        // Assert
        _videoService.Verify(a => a.GetByUserIdAsync(UserId, It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<IReadOnlyCollection<int>>(),
            It.Is<LocationFilter>(l => l.LocationIds.SequenceEqual(new long[] { 12, 40 }) && l.UnknownLocation),
            100, 0, PhotoSortOrder.Asc));
        _videoService.Verify(a => a.GetTotalCountByUserIdAsync(UserId, It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<IReadOnlyCollection<int>>(),
            It.Is<LocationFilter>(l => l.LocationIds.SequenceEqual(new long[] { 12, 40 }) && l.UnknownLocation)));
    }

    [Fact]
    public async Task GetUserVideoLocations_ShouldGiveTheLocationsOfTheVideosOfTheUser()
    {
        // Arrange
        _videoService.Setup(a => a.GetLocationsAsync(UserId)).ReturnsAsync([
            new Location(3, LocationLevel.Country, "Belarus", null, 4),
            new Location(8, LocationLevel.City, "Minsk", "Belarus", 2)
        ]);
        var controller = CreateController();

        // Act
        var result = await controller.GetUserVideoLocations(UserId);

        // Assert
        var locations = Assert.IsType<VideoLocationDto[]>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(["Country", "City"], locations.Select(a => a.Level));
        Assert.Equal("Belarus", locations[1].Description);
        Assert.Equal(2, locations[1].VideoCount);
    }

    [Fact]
    public async Task GetUserVideoYears_ShouldGiveTheYearsOfTheVideosOfTheUser()
    {
        // Arrange
        _videoService.Setup(a => a.GetYearsAsync(UserId)).ReturnsAsync([2016, 2019]);
        var controller = CreateController();

        // Act
        var result = await controller.GetUserVideoYears(UserId);

        // Assert
        Assert.Equal([2016, 2019], Assert.IsAssignableFrom<IReadOnlyList<int>>(Assert.IsType<OkObjectResult>(result).Value));
    }

    private VideosController CreateController()
    {
        var hostInfo = new HostInfo { Scheme = "https", Host = new HostString("localhost", 5001) };

        return new VideosController(_videoService.Object, Mock.Of<IVideoProcessingService>(), Mock.Of<IVideoProvider>(),
            Mock.Of<IFileStorage>(), hostInfo);
    }
}
