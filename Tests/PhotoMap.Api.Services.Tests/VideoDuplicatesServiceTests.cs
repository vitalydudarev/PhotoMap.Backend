using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class VideoDuplicatesServiceTests
{
    private readonly Mock<IVideoService> _videoService = new();

    [Fact]
    public async Task UpdateDuplicateGroupsAsync_ShouldSaveOnlyTheVideosWhoseGroupHasChanged()
    {
        // Arrange: 1 and 2 are grouped already, 3 is a new copy of them, and 4 has lost its copy
        _videoService.Setup(a => a.GetDuplicateKeysAsync()).ReturnsAsync([
            new VideoDuplicateKey(1, 1, "video.mp4", 100, 1),
            new VideoDuplicateKey(2, 1, "video.mp4", 100, 1),
            new VideoDuplicateKey(3, 1, "video.mp4", 100, null),
            new VideoDuplicateKey(4, 1, "other.mp4", 100, 4)
        ]);

        IReadOnlyDictionary<long, long?>? saved = null;
        _videoService
            .Setup(a => a.SetDuplicateGroupsAsync(It.IsAny<IReadOnlyDictionary<long, long?>>()))
            .Callback<IReadOnlyDictionary<long, long?>>(a => saved = a)
            .Returns(Task.CompletedTask);

        var service = new VideoDuplicatesService(_videoService.Object);

        // Act
        var count = await service.UpdateDuplicateGroupsAsync();

        // Assert
        Assert.Equal(2, count);
        Assert.NotNull(saved);
        Assert.Equal(new Dictionary<long, long?> { [3] = 1, [4] = null }, saved);
    }

    [Fact]
    public async Task UpdateDuplicateGroupsAsync_ShouldSaveNothing_WhenNoGroupHasChanged()
    {
        // Arrange
        _videoService.Setup(a => a.GetDuplicateKeysAsync()).ReturnsAsync([
            new VideoDuplicateKey(1, 1, "video.mp4", 100, 1),
            new VideoDuplicateKey(2, 1, "video.mp4", 100, 1),
            new VideoDuplicateKey(3, 1, "other.mp4", 100, null)
        ]);

        var service = new VideoDuplicatesService(_videoService.Object);

        // Act
        var count = await service.UpdateDuplicateGroupsAsync();

        // Assert
        Assert.Equal(0, count);
        _videoService.Verify(a => a.SetDuplicateGroupsAsync(It.IsAny<IReadOnlyDictionary<long, long?>>()), Times.Never);
    }
}
