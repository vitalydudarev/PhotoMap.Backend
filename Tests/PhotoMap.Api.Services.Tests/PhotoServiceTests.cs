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

    [Fact]
    public async Task SetTagsAsync_ShouldCleanTheNames_AndTakeANameGivenTwiceOnce()
    {
        // Arrange
        _photoRepository
            .Setup(a => a.SetTagsAsync(UserId, 5, It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync((long _, long _, IReadOnlyCollection<string> names) => names.ToList());
        var service = CreateService();

        // Act
        var tags = await service.SetTagsAsync(UserId, 5, ["  Paris  trip ", "paris TRIP", "", "   ", "Family"]);

        // Assert
        Assert.Equal(["Paris trip", "Family"], tags);
    }

    [Fact]
    public async Task SuggestTagsAsync_ShouldSuggestNothing_WhenThePhotoIsOfAnotherUser()
    {
        // Arrange
        var photo = CreatePhoto(DateTimeOffset.UtcNow);
        photo.UserId = UserId + 1;
        _photoRepository.Setup(a => a.GetAsync(5)).ReturnsAsync(photo);
        var service = CreateService();

        // Act
        var suggestions = await service.SuggestTagsAsync(UserId, 5);

        // Assert
        Assert.Null(suggestions);
        _photoRepository.Verify(a => a.GetTaggedPhotosAsync(It.IsAny<long>(), It.IsAny<PhotoNeighborhood>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task SuggestTagsAsync_ShouldSuggestTheTagsOfThePhotosAroundIt()
    {
        // Arrange
        var taken = new DateTimeOffset(2019, 7, 14, 12, 0, 0, TimeSpan.Zero);
        var photo = CreatePhoto(taken);
        photo.Id = 5;
        _photoRepository.Setup(a => a.GetAsync(5)).ReturnsAsync(photo);
        _photoRepository
            .Setup(a => a.GetTaggedPhotosAsync(UserId,
                It.Is<PhotoNeighborhood>(n => n.From == taken.AddDays(-7) && n.To == taken.AddDays(7) && n.Box == null),
                It.IsAny<int>()))
            .ReturnsAsync([new TaggedPhoto(6, taken.AddHours(1), null, null, ["Wedding"])]);
        var service = CreateService();

        // Act
        var suggestions = await service.SuggestTagsAsync(UserId, 5);

        // Assert
        Assert.NotNull(suggestions);
        Assert.Equal("Wedding", suggestions[0].Name);
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
