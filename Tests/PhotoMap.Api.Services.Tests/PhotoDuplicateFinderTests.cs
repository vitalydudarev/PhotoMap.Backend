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

    [Fact]
    public void FindDuplicateGroups_ShouldGroupThePhotosNamedAfterTheSameTime_WhateverTheSeparatorsOrTheCase()
    {
        // Act
        var groups = PhotoDuplicateFinder.FindDuplicateGroups([
            Key(1, "a", fileName: "2013-06-09 17.40.57.jpg"),
            Key(2, "b", fileName: "2013-06-09 17-40-57.JPG"),
            Key(3, "c", fileName: "2013-06-09 17.40.57-1.jpg"),
            Key(4, "d", fileName: "2013-06-09 17.40.58.jpg"),
            Key(5, "e", fileName: "IMG_0001.jpg"),
            Key(6, "f", fileName: "img_0001.JPG")
        ]);

        // Assert
        Assert.Equal(1, groups[1]);
        Assert.Equal(1, groups[2]);
        Assert.Null(groups[3]);
        Assert.Null(groups[4]);
        Assert.Null(groups[5]);
        Assert.Null(groups[6]);
    }

    [Fact]
    public void FindDuplicateGroups_ShouldMergeTheGroups_OfThePhotosMatchedByNameAndByContents()
    {
        // Act
        var groups = PhotoDuplicateFinder.FindDuplicateGroups([
            Key(4, "a", fileName: "2013-06-09 17.40.57.jpg"),
            Key(2, "b", fileName: "2013-06-09 17-40-57.jpg"),
            Key(3, "b", fileName: "copy.jpg"),
            Key(1, "c", fileName: "2013-06-09 17-40-57.jpg", userId: 2)
        ]);

        // Assert
        Assert.Equal(2, groups[4]);
        Assert.Equal(2, groups[2]);
        Assert.Equal(2, groups[3]);
        Assert.Null(groups[1]);
    }

    [Fact]
    public void FindDuplicateGroups_ShouldLeaveOutTheDeletedPhotos_MatchedByName()
    {
        // Act
        var groups = PhotoDuplicateFinder.FindDuplicateGroups([
            Key(1, "a", fileName: "2013-06-09 17.40.57.jpg"),
            Key(2, "b", fileName: "2013-06-09 17-40-57.jpg", isDeleted: true)
        ]);

        // Assert
        Assert.Null(groups[1]);
        Assert.Null(groups[2]);
    }

    [Theory]
    [InlineData("2013-06-09 17.40.57.jpg", "2013-06-09 17.40.57.jpg")]
    [InlineData("2013-06-09 17-40-57.JPG", "2013-06-09 17.40.57.jpg")]
    [InlineData("2013-06-09 17-40-57.jpeg", "2013-06-09 17.40.57.jpg")]
    [InlineData("2013-06-09 17.40.57-1.jpg", "2013-06-09 17.40.57-1.jpg")]
    [InlineData("IMG_0001.jpg", null)]
    [InlineData(null, null)]
    public void GetTimeName_ShouldMakeTheSeparatorsAndTheCaseTheSame(string? fileName, string? expected)
    {
        Assert.Equal(expected, PhotoDuplicateFinder.GetTimeName(fileName));
    }

    private static PhotoDuplicateKey Key(long id, string? contentHash, long userId = 1, bool isDeleted = false,
        string? fileName = null)
    {
        return new PhotoDuplicateKey(id, userId, contentHash, isDeleted, null, fileName);
    }
}
