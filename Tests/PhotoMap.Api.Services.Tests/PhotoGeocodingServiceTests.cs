using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class PhotoGeocodingServiceTests
{
    private readonly Mock<IPhotoRepository> _photoRepository = new();
    private readonly Mock<IGeocoder> _geocoder = new();
    private IReadOnlyDictionary<long, string>? _saved;

    public PhotoGeocodingServiceTests()
    {
        _photoRepository
            .Setup(a => a.SetGeocodingsAsync(It.IsAny<IReadOnlyDictionary<long, string>>()))
            .Callback<IReadOnlyDictionary<long, string>>(a => _saved = a.ToDictionary())
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task GeocodeNextAsync_ShouldSaveTheResponsesOfTheGeocoding_ByPhoto()
    {
        // Arrange
        _photoRepository.Setup(a => a.GetNotGeocodedAsync(0, 10))
            .ReturnsAsync([new PhotoLocation(1, 53.9, 27.5), new PhotoLocation(2, 52.2, 21.0)]);
        _geocoder.Setup(a => a.ReverseGeocodeAsync(53.9, 27.5, It.IsAny<CancellationToken>())).ReturnsAsync("minsk");
        _geocoder.Setup(a => a.ReverseGeocodeAsync(52.2, 21.0, It.IsAny<CancellationToken>())).ReturnsAsync("warsaw");

        // Act
        var batch = await CreateService().GeocodeNextAsync(0, 10, CancellationToken.None);

        // Assert
        Assert.Equal(new PhotoGeocodingBatch(2, 2, 0, 2), batch);
        Assert.Equal(new Dictionary<long, string> { [1] = "minsk", [2] = "warsaw" }, _saved);
    }

    [Fact]
    public async Task GeocodeNextAsync_ShouldLeaveThePhotosThatFailed_AndSaveTheRest()
    {
        // Arrange
        _photoRepository.Setup(a => a.GetNotGeocodedAsync(5, 10))
            .ReturnsAsync([new PhotoLocation(6, 1, 1), new PhotoLocation(7, 2, 2)]);
        _geocoder.Setup(a => a.ReverseGeocodeAsync(1, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("500"));
        _geocoder.Setup(a => a.ReverseGeocodeAsync(2, 2, It.IsAny<CancellationToken>())).ReturnsAsync("ok");

        // Act
        var batch = await CreateService().GeocodeNextAsync(5, 10, CancellationToken.None);

        // Assert
        Assert.Equal(new PhotoGeocodingBatch(2, 1, 1, 7), batch);
        Assert.Equal(new Dictionary<long, string> { [7] = "ok" }, _saved);
    }

    [Fact]
    public async Task GeocodeNextAsync_ShouldSaveWhatWasGeocoded_AndThrow_WhenTheGeocodingIsUnavailable()
    {
        // Arrange: one at a time, so the first is geocoded before the second hits the rate limit
        _photoRepository.Setup(a => a.GetNotGeocodedAsync(0, 10))
            .ReturnsAsync([new PhotoLocation(1, 1, 1), new PhotoLocation(2, 2, 2), new PhotoLocation(3, 3, 3)]);
        _geocoder.Setup(a => a.ReverseGeocodeAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync("ok");
        _geocoder.Setup(a => a.ReverseGeocodeAsync(2, 2, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GeocodingUnavailableException("rate limit", TimeSpan.FromSeconds(30)));

        // Act
        var exception = await Assert.ThrowsAsync<GeocodingUnavailableException>(() =>
            CreateService(maxParallelRequests: 1).GeocodeNextAsync(0, 10, CancellationToken.None));

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
        Assert.Equal(new Dictionary<long, string> { [1] = "ok" }, _saved);
        _geocoder.Verify(a => a.ReverseGeocodeAsync(3, 3, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GeocodeNextAsync_ShouldSaveNothing_WhenEveryPhotoIsGeocoded()
    {
        // Arrange
        _photoRepository.Setup(a => a.GetNotGeocodedAsync(3, 10)).ReturnsAsync([]);

        // Act
        var batch = await CreateService().GeocodeNextAsync(3, 10, CancellationToken.None);

        // Assert
        Assert.Equal(0, batch.PhotoCount);
        _photoRepository.Verify(a => a.SetGeocodingsAsync(It.IsAny<IReadOnlyDictionary<long, string>>()), Times.Never);
    }

    private PhotoGeocodingService CreateService(int maxParallelRequests = 8)
    {
        return new PhotoGeocodingService(_photoRepository.Object, _geocoder.Object,
            Options.Create(new MapboxSettings { AccessToken = "token", MaxParallelRequests = maxParallelRequests }),
            NullLogger<PhotoGeocodingService>.Instance);
    }
}
