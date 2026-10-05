using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class PhotoDuplicatesServiceTests
{
    private readonly Mock<IPhotoRepository> _photoRepository = new();

    [Fact]
    public async Task UpdateDuplicateGroupsAsync_ShouldSaveOnlyThePhotosWhoseGroupHasChanged()
    {
        // Arrange: 1 and 2 are grouped already, 3 is a new copy of them, and 4 has lost its copy, 5, to the deleted
        _photoRepository.Setup(a => a.GetDuplicateKeysAsync()).ReturnsAsync([
            new PhotoDuplicateKey(1, 1, "a", false, 1),
            new PhotoDuplicateKey(2, 1, "a", false, 1),
            new PhotoDuplicateKey(3, 1, "a", false, null),
            new PhotoDuplicateKey(4, 1, "b", false, 4),
            new PhotoDuplicateKey(5, 1, "b", true, 4)
        ]);

        IReadOnlyDictionary<long, long?>? saved = null;
        _photoRepository
            .Setup(a => a.SetDuplicateGroupsAsync(It.IsAny<IReadOnlyDictionary<long, long?>>()))
            .Callback<IReadOnlyDictionary<long, long?>>(a => saved = a)
            .Returns(Task.CompletedTask);

        var service = new PhotoDuplicatesService(_photoRepository.Object);

        // Act
        var count = await service.UpdateDuplicateGroupsAsync();

        // Assert
        Assert.Equal(3, count);
        Assert.Equal(new Dictionary<long, long?> { [3] = 1, [4] = null, [5] = null }, saved);
    }

    [Fact]
    public async Task UpdateDuplicateGroupsAsync_ShouldSaveNothing_WhenNoGroupHasChanged()
    {
        // Arrange
        _photoRepository.Setup(a => a.GetDuplicateKeysAsync()).ReturnsAsync([
            new PhotoDuplicateKey(1, 1, "a", false, 1),
            new PhotoDuplicateKey(2, 1, "a", false, 1)
        ]);

        var service = new PhotoDuplicatesService(_photoRepository.Object);

        // Act
        var count = await service.UpdateDuplicateGroupsAsync();

        // Assert
        Assert.Equal(0, count);
        _photoRepository.Verify(a => a.SetDuplicateGroupsAsync(It.IsAny<IReadOnlyDictionary<long, long?>>()), Times.Never);
    }
}
