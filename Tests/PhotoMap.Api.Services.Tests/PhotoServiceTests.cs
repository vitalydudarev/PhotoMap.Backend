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
    private readonly PhotoCategorizationSignal _categorizationSignal = new();
    private readonly PhotoDuplicatesSignal _duplicatesSignal = new();

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
    public async Task AddRangeAsync_ShouldSignalTheCategorization()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.AddRangeAsync([CreatePhoto(DateTimeOffset.UtcNow)]);

        // Assert
        Assert.True(_categorizationSignal.WaitAsync(CancellationToken.None).IsCompletedSuccessfully);
    }

    [Fact]
    public async Task MarkAsDeletedAsync_ShouldMarkThePhotoAsDeletedNow()
    {
        // Arrange
        _photoRepository.Setup(a => a.SetDeletedOnAsync(UserId, 5, It.IsAny<DateTimeOffset?>())).ReturnsAsync(true);
        var service = CreateService();
        var before = DateTimeOffset.UtcNow;

        // Act
        var marked = await service.MarkAsDeletedAsync(UserId, 5);

        // Assert
        Assert.True(marked);
        _photoRepository.Verify(a => a.SetDeletedOnAsync(UserId, 5,
            It.Is<DateTimeOffset?>(d => d >= before && d <= DateTimeOffset.UtcNow)));
    }

    [Fact]
    public async Task RestoreAsync_ShouldMarkThePhotoAsNotDeleted()
    {
        // Arrange
        _photoRepository.Setup(a => a.SetDeletedOnAsync(UserId, 5, null)).ReturnsAsync(true);
        var service = CreateService();

        // Act
        var restored = await service.RestoreAsync(UserId, 5);

        // Assert
        Assert.True(restored);
        _photoRepository.Verify(a => a.SetDeletedOnAsync(UserId, 5, null));
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

    [Fact]
    public async Task AddRangeAsync_ShouldSignalTheSearchForDuplicates()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.AddRangeAsync([CreatePhoto(DateTimeOffset.UtcNow)]);

        // Assert
        Assert.True(_duplicatesSignal.WaitAsync(CancellationToken.None).IsCompletedSuccessfully);
    }

    [Fact]
    public async Task MarkAsDeletedAsync_ShouldSignalTheSearchForDuplicates_WhenThePhotoIsMarked()
    {
        // Arrange
        _photoRepository.Setup(a => a.SetDeletedOnAsync(UserId, 5, It.IsNotNull<DateTimeOffset?>())).ReturnsAsync(true);
        var service = CreateService();

        // Act
        await service.MarkAsDeletedAsync(UserId, 5);

        // Assert
        Assert.True(_duplicatesSignal.WaitAsync(CancellationToken.None).IsCompletedSuccessfully);
    }

    [Fact]
    public async Task RestoreAsync_ShouldNotSignalTheSearchForDuplicates_WhenThereIsNoSuchPhoto()
    {
        // Arrange
        _photoRepository.Setup(a => a.SetDeletedOnAsync(UserId, 5, null)).ReturnsAsync(false);
        var service = CreateService();

        // Act
        await service.RestoreAsync(UserId, 5);

        // Assert
        Assert.False(_duplicatesSignal.WaitAsync(CancellationToken.None).IsCompleted);
    }

    private PhotoService CreateService()
    {
        return new PhotoService(_photoRepository.Object, _yearsCache, _categorizationSignal, _duplicatesSignal);
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
