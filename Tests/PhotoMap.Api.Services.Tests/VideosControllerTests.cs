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
            .Setup(a => a.GetByUserIdAsync(UserId, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateFilter>(),
                It.IsAny<LocationFilter>(), It.IsAny<TagFilter>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<PhotoSortOrder>()))
            .ReturnsAsync([]);
        _videoService
            .Setup(a => a.GetTotalCountByUserIdAsync(UserId, It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<DateFilter>(), It.IsAny<LocationFilter>(), It.IsAny<TagFilter>()))
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
            It.Is<DateFilter>(d => d.Years.SequenceEqual(new[] { 2016, 2019 }) && d.Months.Count == 0), It.IsAny<LocationFilter>(),
            It.IsAny<TagFilter>(), 100, 0, PhotoSortOrder.Desc));
        _videoService.Verify(a => a.GetTotalCountByUserIdAsync(UserId,
            It.Is<IReadOnlyCollection<string>>(f => f.SequenceEqual(new[] { "disk:/Фотокамера" })),
            It.Is<DateFilter>(d => d.Years.SequenceEqual(new[] { 2016, 2019 }) && d.Months.Count == 0), It.IsAny<LocationFilter>(),
            It.IsAny<TagFilter>()));
    }

    [Fact]
    public async Task GetUserVideos_ShouldTakeTheVideosAndTheCountFromTheGivenYearsAndMonths()
    {
        // Arrange
        var controller = CreateController();

        // Act
        await controller.GetUserVideos(UserId, 100, 0, year: [2016], month: [201903, 202011]);

        // Assert
        _videoService.Verify(a => a.GetByUserIdAsync(UserId, It.IsAny<IReadOnlyCollection<string>>(),
            It.Is<DateFilter>(d => d.Years.SequenceEqual(new[] { 2016 }) &&
                                   d.Months.SequenceEqual(new[] { 201903, 202011 })),
            It.IsAny<LocationFilter>(), It.IsAny<TagFilter>(), 100, 0, PhotoSortOrder.Asc));
        _videoService.Verify(a => a.GetTotalCountByUserIdAsync(UserId, It.IsAny<IReadOnlyCollection<string>>(),
            It.Is<DateFilter>(d => d.Months.SequenceEqual(new[] { 201903, 202011 })), It.IsAny<LocationFilter>(),
            It.IsAny<TagFilter>()));
    }

    [Theory]
    [InlineData(2019)]
    [InlineData(201900)]
    [InlineData(201913)]
    public async Task GetUserVideos_ShouldRejectAMonthThatIsNotOne(int month)
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetUserVideos(UserId, 100, 0, month: [month]);

        // Assert
        Assert.IsType<ObjectResult>(result);
        _videoService.Verify(a => a.GetByUserIdAsync(It.IsAny<long>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<DateFilter>(), It.IsAny<LocationFilter>(), It.IsAny<TagFilter>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<PhotoSortOrder>()), Times.Never);
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
            It.Is<DateFilter>(d => !d.FiltersByDate), It.Is<LocationFilter>(l => !l.FiltersByLocations),
            It.Is<TagFilter>(t => !t.FiltersByTags), 100, 0, PhotoSortOrder.Asc));
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
            It.IsAny<DateFilter>(),
            It.Is<LocationFilter>(l => l.LocationIds.SequenceEqual(new long[] { 12, 40 }) && l.UnknownLocation),
            It.IsAny<TagFilter>(), 100, 0, PhotoSortOrder.Asc));
        _videoService.Verify(a => a.GetTotalCountByUserIdAsync(UserId, It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<DateFilter>(),
            It.Is<LocationFilter>(l => l.LocationIds.SequenceEqual(new long[] { 12, 40 }) && l.UnknownLocation),
            It.IsAny<TagFilter>()));
    }

    [Fact]
    public async Task GetUserVideos_ShouldTakeTheVideosAndTheCountOfTheGivenTags()
    {
        // Arrange
        var controller = CreateController();

        // Act
        await controller.GetUserVideos(UserId, 100, 0, tag: [4, 9], untagged: true);

        // Assert
        _videoService.Verify(a => a.GetByUserIdAsync(UserId, It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<DateFilter>(), It.IsAny<LocationFilter>(),
            It.Is<TagFilter>(t => t.TagIds.SequenceEqual(new long[] { 4, 9 }) && t.Untagged), 100, 0,
            PhotoSortOrder.Asc));
        _videoService.Verify(a => a.GetTotalCountByUserIdAsync(UserId, It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<DateFilter>(), It.IsAny<LocationFilter>(),
            It.Is<TagFilter>(t => t.TagIds.SequenceEqual(new long[] { 4, 9 }) && t.Untagged)));
    }

    [Fact]
    public async Task SetVideoTags_ShouldGiveTheTagsOfTheVideo()
    {
        // Arrange
        _videoService.Setup(a => a.SetTagsAsync(UserId, 7, It.Is<IEnumerable<string>>(n =>
                n.SequenceEqual(new[] { "Paris", "Family trip" })))).ReturnsAsync(["Family trip", "Paris"]);
        var controller = CreateController();

        // Act
        var result = await controller.SetVideoTags(UserId, 7, ["  Paris ", "Family   trip", " "]);

        // Assert
        Assert.Equal(new[] { "Family trip", "Paris" },
            Assert.IsAssignableFrom<IReadOnlyList<string>>(Assert.IsType<OkObjectResult>(result).Value));
    }

    [Fact]
    public async Task SetVideoTags_ShouldGiveNotFound_WhenTheUserHasNoSuchVideo()
    {
        // Arrange
        _videoService.Setup(a => a.SetTagsAsync(UserId, 7, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync((IReadOnlyList<string>?)null);
        var controller = CreateController();

        // Act
        var result = await controller.SetVideoTags(UserId, 7, ["Paris"]);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SetVideoTags_ShouldGiveBadRequest_WhenATagIsTooLong()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.SetVideoTags(UserId, 7, [new string('a', TagNames.MaxLength + 1)]);

        // Assert
        Assert.IsType<ObjectResult>(result);
        _videoService.Verify(a => a.SetTagsAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<IEnumerable<string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SetVideoTags_ShouldGiveBadRequest_WhenThereAreTooManyTags()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.SetVideoTags(UserId, 7,
            Enumerable.Range(0, TagNames.MaxPerPhoto + 1).Select(a => $"tag {a}").ToArray());

        // Assert
        Assert.IsType<ObjectResult>(result);
        _videoService.Verify(a => a.SetTagsAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<IEnumerable<string>>()),
            Times.Never);
    }

    [Fact]
    public async Task GetUserVideoLocations_ShouldGiveTheLocationsOfTheVideosOfTheUser()
    {
        // Arrange
        _videoService.Setup(a => a.GetLocationsAsync(UserId)).ReturnsAsync([
            new Location(3, LocationLevel.Country, "Belarus", null, 4, []),
            new Location(8, LocationLevel.City, "Minsk", "Belarus", 2, [3])
        ]);
        var controller = CreateController();

        // Act
        var result = await controller.GetUserVideoLocations(UserId);

        // Assert
        var locations = Assert.IsType<VideoLocationDto[]>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(["Country", "City"], locations.Select(a => a.Level));
        Assert.Equal("Belarus", locations[1].Description);
        Assert.Equal(2, locations[1].VideoCount);
        Assert.Equal([3], locations[1].ParentIds);
        Assert.Empty(locations[0].ParentIds);
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

    [Fact]
    public async Task GetUserVideoFacets_ShouldGiveTheFacetsOfTheVideosTheFilterTakes()
    {
        // Arrange
        var facets = new VideoFacets([new("disk:/Videos", 3)], [new(2019, 3)], [new(12, 1)], 2, [new(4, 1)], 2,
            [new(201903, 3)]);
        _videoService.Setup(a => a.GetFacetsAsync(UserId, It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<DateFilter>(), It.IsAny<LocationFilter>(), It.IsAny<TagFilter>(), It.IsAny<bool>()))
            .ReturnsAsync(facets);
        var controller = CreateController();

        // Act
        var result = await controller.GetUserVideoFacets(UserId, ["disk:/Videos"], [2019], [202003], [12], true, true, [4]);

        // Assert
        Assert.Same(facets, Assert.IsType<OkObjectResult>(result).Value);
        _videoService.Verify(a => a.GetFacetsAsync(UserId,
            It.Is<IReadOnlyCollection<string>>(f => f.SequenceEqual(new[] { "disk:/Videos" })),
            It.Is<DateFilter>(d => d.Years.SequenceEqual(new[] { 2019 }) && d.Months.SequenceEqual(new[] { 202003 })),
            It.Is<LocationFilter>(l => l.LocationIds.SequenceEqual(new long[] { 12 }) && l.UnknownLocation),
            It.Is<TagFilter>(t => t.TagIds.SequenceEqual(new long[] { 4 }) && !t.Untagged), true));
    }

    [Fact]
    public async Task GetUserGeotaggedVideos_ShouldGiveEveryVideoWithAGpsLocationTheFilterTakes()
    {
        // Arrange
        _videoService.Setup(a => a.GetGeotaggedAsync(UserId,
                It.Is<IReadOnlyCollection<string>>(f => f.SequenceEqual(new[] { "disk:/Videos" })),
                It.Is<DateFilter>(d => d.Years.SequenceEqual(new[] { 2019 }) && d.Months.SequenceEqual(new[] { 202003 })),
                It.Is<LocationFilter>(l => l.LocationIds.SequenceEqual(new long[] { 12 }) && !l.UnknownLocation),
                It.Is<TagFilter>(t => !t.FiltersByTags)))
            .ReturnsAsync([new Video
            {
                Id = 7, UserId = UserId, PhotoSourceId = 2, ExternalId = "7", FileName = "trip.mp4", Latitude = 53.9,
                Longitude = 27.56, Tags = ["Paris"]
            }]);
        var controller = CreateController();

        // Act
        var result = await controller.GetUserGeotaggedVideos(UserId, ["disk:/Videos"], [2019], [202003], [12]);

        // Assert
        var videos = Assert.IsType<VideoDto[]>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(7, Assert.Single(videos).Id);
        Assert.Equal(53.9, videos[0].Latitude);
        Assert.Equal(["Paris"], videos[0].Tags);
    }

    [Fact]
    public async Task AddVideoTags_ShouldGiveTheTagsOfEachVideo()
    {
        // Arrange
        _videoService.Setup(a => a.AddTagsAsync(UserId, It.Is<IEnumerable<long>>(ids => ids.SequenceEqual(new long[] { 5, 7 })),
                It.Is<IEnumerable<string>>(n => n.SequenceEqual(new[] { "Paris" }))))
            .ReturnsAsync(AddedTags.Added(new Dictionary<long, IReadOnlyList<string>>
            {
                [5] = ["Paris"],
                [7] = ["Family", "Paris"]
            }));
        var controller = CreateController();

        // Act
        var result = await controller.AddVideoTags(UserId,
            new AddVideoTagsDto { VideoIds = [5, 7, 5], Tags = ["  Paris ", " "] });

        // Assert
        var videos = Assert.IsType<VideoTagsDto[]>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal([(5L, "Paris"), (7L, "Family,Paris")],
            videos.Select(a => (a.VideoId, string.Join(",", a.Tags))).OrderBy(a => a.VideoId).ToArray());
    }

    [Fact]
    public async Task AddVideoTags_ShouldGiveNotFound_WhenTheUserHasNoSuchVideo()
    {
        // Arrange
        _videoService.Setup(a => a.AddTagsAsync(UserId, It.IsAny<IEnumerable<long>>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(AddedTags.PhotoNotFound);
        var controller = CreateController();

        // Act
        var result = await controller.AddVideoTags(UserId, new AddVideoTagsDto { VideoIds = [5], Tags = ["Paris"] });

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AddVideoTags_ShouldGiveBadRequest_WhenAVideoWouldHaveTooManyTags()
    {
        // Arrange
        _videoService.Setup(a => a.AddTagsAsync(UserId, It.IsAny<IEnumerable<long>>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(AddedTags.TooManyTags);
        var controller = CreateController();

        // Act
        var result = await controller.AddVideoTags(UserId, new AddVideoTagsDto { VideoIds = [5], Tags = ["Paris"] });

        // Assert
        Assert.IsType<ObjectResult>(result);
    }

    [Theory]
    [InlineData(new long[0], new[] { "Paris" })]
    [InlineData(new long[] { 5 }, new[] { " " })]
    public async Task AddVideoTags_ShouldGiveBadRequest_WithNoVideoOrNoTag(long[] videoIds, string[] tags)
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.AddVideoTags(UserId, new AddVideoTagsDto { VideoIds = videoIds, Tags = tags });

        // Assert
        Assert.IsType<ObjectResult>(result);
        _videoService.Verify(a => a.AddTagsAsync(It.IsAny<long>(), It.IsAny<IEnumerable<long>>(),
            It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task GetVideoTagSuggestions_ShouldGiveTheTagsSuggestedForTheVideo()
    {
        // Arrange
        _videoService.Setup(a => a.SuggestTagsAsync(UserId, 7))
            .ReturnsAsync([new TagSuggestion("Minsk", false, true), new TagSuggestion("2019", true, false)]);
        var controller = CreateController();

        // Act
        var result = await controller.GetVideoTagSuggestions(UserId, 7);

        // Assert
        var suggestions = Assert.IsType<TagSuggestionDto[]>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal([("Minsk", false, true), ("2019", true, false)],
            suggestions.Select(a => (a.Name, a.ByDate, a.ByLocation)).ToArray());
    }

    [Fact]
    public async Task GetVideoTagSuggestions_ShouldGiveNotFound_WhenTheUserHasNoSuchVideo()
    {
        // Arrange
        _videoService.Setup(a => a.SuggestTagsAsync(UserId, 7)).ReturnsAsync((IReadOnlyList<TagSuggestion>?)null);
        var controller = CreateController();

        // Act
        var result = await controller.GetVideoTagSuggestions(UserId, 7);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    private VideosController CreateController()
    {
        var hostInfo = new HostInfo { Scheme = "https", Host = new HostString("localhost", 5001) };

        return new VideosController(_videoService.Object, Mock.Of<IVideoProcessingService>(), Mock.Of<IVideoProvider>(),
            Mock.Of<IFileStorage>(), Mock.Of<IFailedFileService>(), hostInfo);
    }
}
