using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class VideoDuplicateFinderTests
{
    [Fact]
    public void FindDuplicateGroups_ShouldGroupTheVideosOfTheSameSizeAndName_ByTheFirstOfThem()
    {
        // Act
        var groups = VideoDuplicateFinder.FindDuplicateGroups([
            Key(3, "video.mp4", 100),
            Key(1, "video.mp4", 100),
            Key(2, "VIDEO.MP4", 100)
        ]);

        // Assert
        Assert.Equal(1, groups[1]);
        Assert.Equal(1, groups[2]);
        Assert.Equal(1, groups[3]);
    }

    [Fact]
    public void FindDuplicateGroups_ShouldLeaveOutTheVideosOfAnotherSizeOrName()
    {
        // Act
        var groups = VideoDuplicateFinder.FindDuplicateGroups([
            Key(1, "video.mp4", 100),
            Key(2, "video.mp4", 101),
            Key(3, "other.mp4", 100)
        ]);

        // Assert
        Assert.Null(groups[1]);
        Assert.Null(groups[2]);
        Assert.Null(groups[3]);
    }

    [Fact]
    public void FindDuplicateGroups_ShouldNotGroupTheVideosOfDifferentUsers()
    {
        // Act
        var groups = VideoDuplicateFinder.FindDuplicateGroups([
            Key(1, "video.mp4", 100, userId: 1),
            Key(2, "video.mp4", 100, userId: 2)
        ]);

        // Assert
        Assert.Null(groups[1]);
        Assert.Null(groups[2]);
    }

    private static VideoDuplicateKey Key(long id, string fileName, long size, long userId = 1, long? duplicateGroupId = null)
    {
        return new VideoDuplicateKey(id, userId, fileName, size, duplicateGroupId);
    }
}
