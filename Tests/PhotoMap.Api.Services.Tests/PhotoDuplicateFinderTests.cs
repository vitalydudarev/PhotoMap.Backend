using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class PhotoDuplicateFinderTests
{
    [Fact]
    public void FindDuplicateGroups_ShouldGroupThePhotosOfTheSameContents_ByTheFirstOfThem()
    {
        // Act
        var groups = PhotoDuplicateFinder.FindDuplicateGroups([Key(3, "a"), Key(1, "a"), Key(2, "a"), Key(4, "b")]);

        // Assert
        Assert.Equal(1, groups[1]);
        Assert.Equal(1, groups[2]);
        Assert.Equal(1, groups[3]);
        Assert.Null(groups[4]);
    }

    [Fact]
    public void FindDuplicateGroups_ShouldLeaveOutTheDeletedPhotos_AndTheOnesLeftAloneByThem()
    {
        // Act
        var groups = PhotoDuplicateFinder.FindDuplicateGroups([Key(1, "a"), Key(2, "a", isDeleted: true)]);

        // Assert
        Assert.Null(groups[1]);
        Assert.Null(groups[2]);
    }

    [Fact]
    public void FindDuplicateGroups_ShouldNotGroupThePhotosWithoutAHash_OrOfDifferentUsers()
    {
        // Act
        var groups = PhotoDuplicateFinder.FindDuplicateGroups([
            Key(1, null),
            Key(2, null),
            Key(3, "a", userId: 1),
            Key(4, "a", userId: 2)
        ]);

        // Assert
        Assert.All(groups.Values, Assert.Null);
    }

    private static PhotoDuplicateKey Key(long id, string? contentHash, long userId = 1, bool isDeleted = false)
    {
        return new PhotoDuplicateKey(id, userId, contentHash, isDeleted, null);
    }
}
