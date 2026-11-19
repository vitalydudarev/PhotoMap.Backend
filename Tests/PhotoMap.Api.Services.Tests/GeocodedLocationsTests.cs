using System.Text.Json;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class GeocodedLocationsTests
{
    private static readonly object Belarus = new { name = "Belarus", mapbox_id = "country.by", country_code = "by" };
    private static readonly object MinskRegion = new { name = "Minsk Region", mapbox_id = "region.minsk" };
    private static readonly object Minsk = new { name = "Minsk", mapbox_id = "place.minsk" };
    private static readonly object Frunzensky = new { name = "Frunzensky District", mapbox_id = "locality.frunzensky" };
    private static readonly object Postcode = new { name = "220073", mapbox_id = "postcode.1" };

    [Fact]
    public void Get_ShouldReturnTheLocationsOfTheContext_FromTheCountryDown()
    {
        // Arrange
        var geocoding = Features(Context(country: Belarus, region: MinskRegion, place: Minsk, locality: Frunzensky,
            postcode: Postcode));

        // Act
        var locations = GeocodedLocations.Get(geocoding);

        // Assert
        Assert.Equal(
            [
                new GeocodedLocation(LocationLevel.Country, "country.by", "Belarus", null),
                new GeocodedLocation(LocationLevel.Region, "region.minsk", "Minsk Region", "Belarus"),
                new GeocodedLocation(LocationLevel.City, "place.minsk", "Minsk", "Minsk Region, Belarus"),
                new GeocodedLocation(LocationLevel.Locality, "locality.frunzensky", "Frunzensky District",
                    "Minsk, Belarus"),
                new GeocodedLocation(LocationLevel.Postcode, "BY:220073", "220073", "Minsk, Belarus")
            ],
            locations);
    }

    [Fact]
    public void Get_ShouldTellAPostcodeByItsCountryAndName_RatherThanItsMapboxId()
    {
        // Arrange: Mapbox gives the same ID to different postcodes
        var first = Features(Context(country: Belarus, postcode: new { name = "220073", mapbox_id = "postcode.1" }));
        var second = Features(Context(country: Belarus, postcode: new { name = "220074", mapbox_id = "postcode.1" }));

        // Act
        var firstKey = GeocodedLocations.Get(first).Single(a => a.Level == LocationLevel.Postcode).Key;
        var secondKey = GeocodedLocations.Get(second).Single(a => a.Level == LocationLevel.Postcode).Key;

        // Assert
        Assert.Equal("BY:220073", firstKey);
        Assert.Equal("BY:220074", secondKey);
    }

    [Fact]
    public void Get_ShouldLeaveTheRegionOutOfTheDescriptionOfACity_WhenTheyHaveTheSameName()
    {
        // Arrange
        var geocoding = Features(Context(country: Belarus, region: new { name = "Minsk", mapbox_id = "region.minsk" },
            place: Minsk));

        // Act
        var city = GeocodedLocations.Get(geocoding).Single(a => a.Level == LocationLevel.City);

        // Assert
        Assert.Equal("Belarus", city.Description);
    }

    [Fact]
    public void Get_ShouldTakeALevelFromTheNextFeature_WhenTheFirstHasNone()
    {
        // Arrange
        var geocoding = Features(Context(country: Belarus, place: Minsk), Context(country: Belarus,
            place: Minsk, locality: Frunzensky));

        // Act
        var locations = GeocodedLocations.Get(geocoding);

        // Assert
        Assert.Equal([LocationLevel.Country, LocationLevel.City, LocationLevel.Locality],
            locations.Select(a => a.Level));
    }

    [Fact]
    public void Get_ShouldSkipTheLevelsWithNoName()
    {
        // Arrange
        var geocoding = Features(Context(country: Belarus, place: new { name = " ", mapbox_id = "place.x" }));

        // Act
        var locations = GeocodedLocations.Get(geocoding);

        // Assert
        Assert.Equal(LocationLevel.Country, Assert.Single(locations).Level);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":[]}")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"properties\":{}}]}")]
    public void Get_ShouldReturnNoLocation_WhenThereIsNone(string? geocoding)
    {
        // Act
        var locations = GeocodedLocations.Get(geocoding);

        // Assert
        Assert.Empty(locations);
    }

    private static string Features(params object[] contexts)
    {
        return JsonSerializer.Serialize(new
        {
            type = "FeatureCollection",
            features = contexts.Select(context => new { type = "Feature", properties = new { context } })
        });
    }

    private static Dictionary<string, object> Context(object? country = null, object? region = null,
        object? place = null, object? locality = null, object? postcode = null)
    {
        var context = new Dictionary<string, object?>
        {
            ["country"] = country,
            ["region"] = region,
            ["place"] = place,
            ["locality"] = locality,
            ["postcode"] = postcode
        };

        return context.Where(a => a.Value != null).ToDictionary(a => a.Key, a => a.Value!);
    }
}
