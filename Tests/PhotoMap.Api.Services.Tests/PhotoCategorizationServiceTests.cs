using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class PhotoCategorizationServiceTests
{
    private readonly Mock<IPhotoRepository> _photoRepository = new();

    [Fact]
    public async Task CategorizeNextAsync_ShouldSaveTheCategoriesOfThePhotos_WithTheVersionOfTheRules()
    {
        // Arrange
        _photoRepository
            .Setup(a => a.GetNotCategorizedAsync(PhotoCategorizer.Version, 10))
            .ReturnsAsync([CreatePhoto(1, "shot.png", null), CreatePhoto(2, "IMG_0001.jpg", null)]);

        IReadOnlyDictionary<long, IReadOnlyCollection<PhotoCategory>>? saved = null;
        _photoRepository
            .Setup(a => a.SetCategoriesAsync(It.IsAny<IReadOnlyDictionary<long, IReadOnlyCollection<PhotoCategory>>>(),
                PhotoCategorizer.Version))
            .Callback<IReadOnlyDictionary<long, IReadOnlyCollection<PhotoCategory>>, int>((a, _) => saved = a)
            .Returns(Task.CompletedTask);

        var service = new PhotoCategorizationService(_photoRepository.Object);

        // Act
        var count = await service.CategorizeNextAsync(10);

        // Assert
        Assert.Equal(2, count);
        Assert.NotNull(saved);
        Assert.Equal([PhotoCategory.Screenshot], saved[1]);
        Assert.Empty(saved[2]);
    }

    [Fact]
    public async Task CategorizeNextAsync_ShouldSaveNothing_WhenEveryPhotoIsCategorized()
    {
        // Arrange
        _photoRepository.Setup(a => a.GetNotCategorizedAsync(PhotoCategorizer.Version, 10)).ReturnsAsync([]);
        var service = new PhotoCategorizationService(_photoRepository.Object);

        // Act
        var count = await service.CategorizeNextAsync(10);

        // Assert
        Assert.Equal(0, count);
        _photoRepository.Verify(a => a.SetCategoriesAsync(
            It.IsAny<IReadOnlyDictionary<long, IReadOnlyCollection<PhotoCategory>>>(), It.IsAny<int>()), Times.Never);
    }

    private static Photo CreatePhoto(long id, string fileName, string? exifString)
    {
        return new Photo
        {
            Id = id,
            UserId = 1,
            PhotoSourceId = 1,
            FileName = fileName,
            ExifString = exifString,
            AddedOn = DateTimeOffset.UtcNow
        };
    }
}
