using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class VideoGeocodingServiceTests
{
    private readonly Mock<IVideoService> _videoService = new();
    private readonly Mock<IGeocoder> _geocoder = new();
    private IReadOnlyDictionary<long, PhotoGeocoding>? _saved;

    public VideoGeocodingServiceTests()
    {
        _videoService
            .Setup(a => a.SetGeocodingsAsync(It.IsAny<IReadOnlyDictionary<long, PhotoGeocoding>>()))
            .Callback<IReadOnlyDictionary<long, PhotoGeocoding>>(a => _saved = a.ToDictionary())
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task GeocodeNextAsync_ShouldSaveTheResponsesOfTheGeocoding_AndTheLocationsTheyTell_ByVideo()
    {
        // Arrange
        const string response = """
            {"type":"FeatureCollection","features":[{"type":"Feature","properties":{"context":{
              "country":{"name":"Belarus","mapbox_id":"by","country_code":"BY"},
              "place":{"name":"Minsk","mapbox_id":"minsk"}}}}]}
            """;
        _videoService.Setup(a => a.GetNotGeocodedAsync(0, 10))
            .ReturnsAsync([new VideoGpsLocation(1, 53.9, 27.5), new VideoGpsLocation(2, 52.2, 21.0)]);
        _geocoder.Setup(a => a.ReverseGeocodeAsync(53.9, 27.5, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        _geocoder.Setup(a => a.ReverseGeocodeAsync(52.2, 21.0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("failed"));

        // Act
        var batch = await CreateService().GeocodeNextAsync(0, 10, CancellationToken.None);

        // Assert
        Assert.Equal(new VideoGeocodingBatch(2, 1, 1, 2), batch);
        Assert.Equal([1L], _saved!.Keys);
        Assert.Equal(response, _saved[1].Response);
        Assert.Equal(["Belarus", "Minsk"], _saved[1].Locations.Select(a => a.Name));
    }

    [Fact]
    public async Task GeocodeNextAsync_ShouldSaveWhatWasGeocoded_AndThrow_WhenTheGeocodingIsUnavailable()
    {
        // Arrange: one at a time, so the first is geocoded before the second hits the rate limit
        _videoService.Setup(a => a.GetNotGeocodedAsync(0, 10))
            .ReturnsAsync([new VideoGpsLocation(1, 1, 1), new VideoGpsLocation(2, 2, 2)]);
        _geocoder.Setup(a => a.ReverseGeocodeAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync("ok");
        _geocoder.Setup(a => a.ReverseGeocodeAsync(2, 2, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GeocodingUnavailableException("rate limit", TimeSpan.FromSeconds(30)));

        // Act
        await Assert.ThrowsAsync<GeocodingUnavailableException>(() =>
            CreateService(maxParallelRequests: 1).GeocodeNextAsync(0, 10, CancellationToken.None));

        // Assert
        Assert.Equal([1L], _saved!.Keys);
    }

    [Fact]
    public async Task GeocodeNextAsync_ShouldSaveNothing_WhenEveryVideoIsGeocoded()
    {
        // Arrange
        _videoService.Setup(a => a.GetNotGeocodedAsync(3, 10)).ReturnsAsync([]);

        // Act
        var batch = await CreateService().GeocodeNextAsync(3, 10, CancellationToken.None);

        // Assert
        Assert.Equal(new VideoGeocodingBatch(0, 0, 0, 3), batch);
        _videoService.Verify(a => a.SetGeocodingsAsync(It.IsAny<IReadOnlyDictionary<long, PhotoGeocoding>>()),
            Times.Never);
    }

    private VideoGeocodingService CreateService(int maxParallelRequests = 8)
    {
        return new VideoGeocodingService(_videoService.Object, _geocoder.Object,
            Options.Create(new MapboxSettings { AccessToken = "token", MaxParallelRequests = maxParallelRequests }),
            NullLogger<VideoGeocodingService>.Instance);
    }
}
