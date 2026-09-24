using Moq;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class ImageStoreTests
{
    private static readonly byte[] Thumbnail = [1, 2, 3];

    private readonly Mock<IFileStorage> _fileStorage = new();

    [Fact]
    public async Task SaveThumbnailAsync_ShouldSaveFilesOfSameNameApart()
    {
        // Arrange
        var imageStore = new ImageStore(_fileStorage.Object);

        // Act
        var firstPath = await imageStore.SaveThumbnailAsync(Thumbnail, "IMG_0001.JPG", "id:first", "1", "Dropbox", 256);
        var secondPath = await imageStore.SaveThumbnailAsync(Thumbnail, "IMG_0001.JPG", "id:second", "1", "Dropbox", 256);

        // Assert
        Assert.NotEqual(firstPath, secondPath);
        _fileStorage.Verify(a => a.SaveAsync(firstPath, Thumbnail));
        _fileStorage.Verify(a => a.SaveAsync(secondPath, Thumbnail));
    }

    [Fact]
    public async Task SaveThumbnailAsync_ShouldSaveSameFileToSamePath()
    {
        // Arrange
        var imageStore = new ImageStore(_fileStorage.Object);

        // Act
        var firstPath = await imageStore.SaveThumbnailAsync(Thumbnail, "IMG_0001.JPG", "id:first", "1", "Dropbox", 256);
        var secondPath = await imageStore.SaveThumbnailAsync(Thumbnail, "IMG_0001.JPG", "id:first", "1", "Dropbox", 256);

        // Assert
        Assert.Equal(firstPath, secondPath);
    }

    [Fact]
    public async Task SaveThumbnailAsync_ShouldNameThumbnailAfterFileAndSize()
    {
        // Arrange
        var imageStore = new ImageStore(_fileStorage.Object);

        // Act
        var path = await imageStore.SaveThumbnailAsync(Thumbnail, "IMG_0001.HEIC", "id:first", "1", "Dropbox", 256);

        // Assert
        Assert.StartsWith(Path.Combine("Dropbox", "1", "thumbs", "IMG_0001_"), path);
        Assert.EndsWith("_256.jpg", path);
    }

    [Fact]
    public async Task SaveThumbnailAsync_ShouldSaveFilesWithoutIdApart()
    {
        // Arrange
        var imageStore = new ImageStore(_fileStorage.Object);

        // Act
        var firstPath = await imageStore.SaveThumbnailAsync(Thumbnail, "IMG_0001.JPG", null, "1", "Dropbox", 256);
        var secondPath = await imageStore.SaveThumbnailAsync(Thumbnail, "IMG_0001.JPG", null, "1", "Dropbox", 256);

        // Assert
        Assert.NotEqual(firstPath, secondPath);
    }
}
