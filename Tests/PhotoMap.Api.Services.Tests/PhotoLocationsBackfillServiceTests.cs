using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class PhotoLocationsBackfillServiceTests
{
    private const string MinskGeocoding = """
        {"type":"FeatureCollection","features":[{"type":"Feature","properties":{"context":{
          "country":{"name":"Belarus","mapbox_id":"by","country_code":"BY"}}}}]}
        """;

    private const string NothingFound = """{"type":"FeatureCollection","features":[]}""";

    private readonly Mock<IPhotoRepository> _photoRepository = new();

    [Fact]
    public async Task BackfillNextAsync_ShouldSaveTheLocationsOfTheNextPhotos()
    {
        // Arrange
        IReadOnlyDictionary<long, IReadOnlyCollection<GeocodedLocation>>? saved = null;
        _photoRepository.Setup(a => a.GetGeocodedAsync(10, 500)).ReturnsAsync([(11, MinskGeocoding), (14, NothingFound)]);
        _photoRepository
            .Setup(a => a.SetLocationsAsync(It.IsAny<IReadOnlyDictionary<long, IReadOnlyCollection<GeocodedLocation>>>()))
            .Callback<IReadOnlyDictionary<long, IReadOnlyCollection<GeocodedLocation>>>(a => saved = a)
            .Returns(Task.CompletedTask);

        // Act
        var (lastId, locatedCount) = await new PhotoLocationsBackfillService(_photoRepository.Object)
            .BackfillNextAsync(10, 500);

        // Assert
        Assert.Equal(14, lastId);
        Assert.Equal(1, locatedCount);
        Assert.Equal([new GeocodedLocation(LocationLevel.Country, "by", "Belarus", null)], saved![11]);
        // a photo where nothing was found is taken off any location it had
        Assert.Empty(saved[14]);
    }

    [Fact]
    public async Task BackfillNextAsync_ShouldReturnNoId_WhenNoPhotoIsLeft()
    {
        // Arrange
        _photoRepository.Setup(a => a.GetGeocodedAsync(14, 500)).ReturnsAsync([]);

        // Act
        var (lastId, _) = await new PhotoLocationsBackfillService(_photoRepository.Object).BackfillNextAsync(14, 500);

        // Assert
        Assert.Null(lastId);
        _photoRepository.Verify(
            a => a.SetLocationsAsync(It.IsAny<IReadOnlyDictionary<long, IReadOnlyCollection<GeocodedLocation>>>()),
            Times.Never);
    }
}
