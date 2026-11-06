using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class PhotoTagSuggesterTests
{
    private static readonly DateTimeOffset Taken = new(2019, 7, 14, 12, 0, 0, TimeSpan.Zero);

    // Minsk, and a point about 1 km north of it
    private const double Latitude = 53.9;
    private const double Longitude = 27.56;
    private const double OneKilometerNorth = 53.909;

    [Fact]
    public void Suggest_ShouldSuggestTheTagsOfThePhotosTakenAroundTheSameTime_TheNearestFirst()
    {
        // Arrange
        var photo = CreatePhoto(Taken);
        TaggedPhoto[] tagged =
        [
            new(2, Taken.AddDays(5), null, null, ["Holiday"]),
            new(3, Taken.AddHours(2), null, null, ["Wedding"]),
            new(4, Taken.AddDays(8), null, null, ["Too late"])
        ];

        // Act
        var suggestions = PhotoTagSuggester.Suggest(photo, tagged);

        // Assert
        Assert.Equal(["Wedding", "Holiday"], NeighborTags(suggestions));
        Assert.All(suggestions, a => Assert.True(a.ByDate));
        Assert.All(suggestions, a => Assert.False(a.ByLocation));
    }

    [Fact]
    public void Suggest_ShouldSuggestTheTagsOfThePhotosTakenNearby_WhateverWhen()
    {
        // Arrange
        var photo = CreatePhoto(Taken, Latitude, Longitude);
        TaggedPhoto[] tagged =
        [
            new(2, Taken.AddYears(-3), OneKilometerNorth, Longitude, ["Home"]),
            // about 11 km away
            new(3, Taken.AddYears(-3), Latitude + 0.1, Longitude, ["Far away"])
        ];

        // Act
        var suggestions = PhotoTagSuggester.Suggest(photo, tagged);

        // Assert
        var home = Assert.Single(suggestions, a => a.Name == "Home");
        Assert.True(home.ByLocation);
        Assert.False(home.ByDate);
        Assert.DoesNotContain(suggestions, a => a.Name == "Far away");
    }

    [Fact]
    public void Suggest_ShouldPutFirstTheTagsOfThePhotosTakenAtTheSameTimeAndPlace()
    {
        // Arrange
        var photo = CreatePhoto(Taken, Latitude, Longitude);
        TaggedPhoto[] tagged =
        [
            new(2, Taken.AddHours(1), null, null, ["Same time"]),
            new(3, Taken.AddYears(-1), Latitude, Longitude, ["Same place"]),
            new(4, Taken.AddHours(1), Latitude, Longitude, ["Both"])
        ];

        // Act
        var suggestions = PhotoTagSuggester.Suggest(photo, tagged);

        // Assert
        Assert.Equal("Both", suggestions[0].Name);
        Assert.True(suggestions[0].ByDate);
        Assert.True(suggestions[0].ByLocation);
    }

    [Fact]
    public void Suggest_ShouldAddUpTheTagOfManyPhotos_AndTakeItOnceWhateverItsCase()
    {
        // Arrange
        var photo = CreatePhoto(Taken);
        TaggedPhoto[] tagged =
        [
            new(2, Taken.AddHours(1), null, null, ["Once"]),
            new(3, Taken.AddDays(1), null, null, ["Paris"]),
            new(4, Taken.AddDays(1), null, null, ["paris"]),
            new(5, Taken.AddDays(1), null, null, ["PARIS"])
        ];

        // Act
        var suggestions = PhotoTagSuggester.Suggest(photo, tagged);

        // Assert
        Assert.Equal(["Paris", "Once"], NeighborTags(suggestions));
    }

    [Fact]
    public void Suggest_ShouldLeaveOutTheTagsThePhotoHas()
    {
        // Arrange
        var photo = CreatePhoto(Taken);
        TaggedPhoto[] tagged =
        [
            // the photo itself is among the tagged photos around it
            new(1, Taken, null, null, ["Wedding", "2019"]),
            new(2, Taken.AddHours(1), null, null, ["wedding", "Party"])
        ];

        // Act
        var suggestions = PhotoTagSuggester.Suggest(photo, tagged);

        // Assert
        Assert.Equal(["Party", "Summer 2019"], suggestions.Select(a => a.Name));
    }

    [Fact]
    public void Suggest_ShouldSuggestTheYearAndTheSeason_WhenNoPhotoAroundHasTags()
    {
        // Act
        var suggestions = PhotoTagSuggester.Suggest(CreatePhoto(Taken), []);

        // Assert
        Assert.Equal([new TagSuggestion("2019", true, false), new TagSuggestion("Summer 2019", true, false)], suggestions);
    }

    [Fact]
    public void Suggest_ShouldSuggestTenTagsAtMost()
    {
        // Arrange
        var tagged = Enumerable.Range(2, 20)
            .Select(a => new TaggedPhoto(a, Taken.AddHours(a), null, null, [$"Tag {a}"]))
            .ToArray();

        // Act
        var suggestions = PhotoTagSuggester.Suggest(CreatePhoto(Taken), tagged);

        // Assert
        Assert.Equal(PhotoTagSuggester.MaxSuggestions, suggestions.Count);
        Assert.Equal("Tag 2", suggestions[0].Name);
    }

    [Theory]
    [InlineData(2019, 3, Latitude, "Spring 2019")]
    [InlineData(2019, 11, Latitude, "Autumn 2019")]
    [InlineData(2019, 12, Latitude, "Winter 2019–2020")]
    [InlineData(2020, 2, Latitude, "Winter 2019–2020")]
    // the seasons of the south are the other way round
    [InlineData(2019, 7, -33.9, "Winter 2019")]
    [InlineData(2020, 1, -33.9, "Summer 2019–2020")]
    public void GetDateTags_ShouldTellTheYearAndTheSeason(int year, int month, double latitude, string season)
    {
        // Arrange
        var photo = CreatePhoto(new DateTimeOffset(year, month, 10, 12, 0, 0, TimeSpan.Zero), latitude, Longitude);

        // Act
        var tags = PhotoTagSuggester.GetDateTags(photo);

        // Assert
        Assert.Equal([year.ToString(), season], tags);
    }

    [Fact]
    public void GetNeighborhood_ShouldTakeAWeekAroundThePhoto_AndABoxOf5KilometersAroundItsLocation()
    {
        // Act
        var neighborhood = PhotoTagSuggester.GetNeighborhood(CreatePhoto(Taken, Latitude, Longitude));

        // Assert
        Assert.Equal(Taken.AddDays(-7), neighborhood.From);
        Assert.Equal(Taken.AddDays(7), neighborhood.To);

        var box = Assert.IsType<GeoBox>(neighborhood.Box);
        Assert.Equal(5, PhotoTagSuggester.GetKilometersApart(Latitude, Longitude, box.MaxLatitude, Longitude), 1);
        Assert.Equal(5, PhotoTagSuggester.GetKilometersApart(Latitude, Longitude, Latitude, box.MaxLongitude), 1);
    }

    [Fact]
    public void GetNeighborhood_ShouldTakeNoBox_WhenThePhotoHasNoLocation()
    {
        // Act
        var neighborhood = PhotoTagSuggester.GetNeighborhood(CreatePhoto(Taken));

        // Assert
        Assert.Null(neighborhood.Box);
    }

    /// <summary>
    /// The suggestions but the year and the season, which follow them.
    /// </summary>
    private static IEnumerable<string> NeighborTags(IEnumerable<TagSuggestion> suggestions)
    {
        return suggestions.Select(a => a.Name).Where(a => a != "2019" && a != "Summer 2019");
    }

    private static Photo CreatePhoto(DateTimeOffset taken, double? latitude = null, double? longitude = null)
    {
        return new Photo
        {
            Id = 1,
            UserId = 1,
            FileName = "photo.jpg",
            DateTimeTaken = taken,
            Latitude = latitude,
            Longitude = longitude,
            HasGps = latitude != null && longitude != null
        };
    }
}
