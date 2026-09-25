using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Services.Services;
using PhotoMap.Api.Services.Services.Domain;

namespace PhotoMap.Api.Services.Tests;

public class PhotoServiceTests
{
    private const long UserId = 1;

    private readonly Mock<IPhotoRepository> _photoRepository = new();
    private readonly PhotoYearsCache _yearsCache = new();

    public PhotoServiceTests()
    {
        _photoRepository.Setup(a => a.GetYearsAsync(UserId)).ReturnsAsync([2016]);
        _photoRepository.Setup(a => a.DeleteByPhotoSourceAsync(UserId, It.IsAny<long>())).ReturnsAsync([]);
    }

    [Fact]
    public async Task AddRangeAsync_ShouldAddTheYearsOfThePhotos_InUtc()
    {
        // Arrange
        var service = CreateService();
        await service.GetYearsAsync(UserId);

        // Act: taken on New Year's Eve in a time zone ahead of UTC, so still in 2020 in UTC
        await service.AddRangeAsync([CreatePhoto(new DateTimeOffset(2021, 1, 1, 1, 0, 0, TimeSpan.FromHours(3)))]);

        // Assert
        Assert.Equal([2016, 2020], await service.GetYearsAsync(UserId));
        _photoRepository.Verify(a => a.GetYearsAsync(UserId), Times.Once);
    }

    [Fact]
    public async Task DeleteByPhotoSourceAsync_ShouldReadTheYearsAgain()
    {
        // Arrange
        var service = CreateService();
        await service.GetYearsAsync(UserId);

        // Act
        await service.DeleteByPhotoSourceAsync(UserId, 1);
        await service.GetYearsAsync(UserId);

        // Assert
        _photoRepository.Verify(a => a.GetYearsAsync(UserId), Times.Exactly(2));
    }

    private PhotoService CreateService()
    {
        return new PhotoService(_photoRepository.Object, _yearsCache);
    }

    private static Photo CreatePhoto(DateTimeOffset dateTimeTaken)
    {
        return new Photo
        {
            UserId = UserId,
            PhotoSourceId = 1,
            FileName = "photo.jpg",
            DateTimeTaken = dateTimeTaken,
            AddedOn = DateTimeOffset.UtcNow
        };
    }
}
