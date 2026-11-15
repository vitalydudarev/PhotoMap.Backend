using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class GeocodedAddressTests
{
    [Fact]
    public void Get_ShouldReturnTheFullAddressOfTheAddressFeature()
    {
        // Arrange
        var geocoding = Features(
            Feature("street", fullAddress: "Main Street, Springfield"),
            Feature("address", fullAddress: "12 Main Street, Springfield"),
            Feature("place", fullAddress: "Springfield"));

        // Act
        var address = GeocodedAddress.Get(geocoding);

        // Assert
        Assert.Equal("12 Main Street, Springfield", address);
    }

    [Fact]
    public void Get_ShouldReturnTheAddressOfTheFirstFeature_WhenThereIsNoAddressFeature()
    {
        // Arrange
        var geocoding = Features(
            Feature("street", fullAddress: "Main Street, Springfield"),
            Feature("place", fullAddress: "Springfield"));

        // Act
        var address = GeocodedAddress.Get(geocoding);

        // Assert
        Assert.Equal("Main Street, Springfield", address);
    }

    [Fact]
    public void Get_ShouldReturnTheNameAndThePlace_WhenTheFeatureHasNoFullAddress()
    {
        // Arrange
        var geocoding = Features(Feature("poi", name: "Central Park", placeFormatted: "New York, United States"));

        // Act
        var address = GeocodedAddress.Get(geocoding);

        // Assert
        Assert.Equal("Central Park, New York, United States", address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":[]}")]
    public void Get_ShouldReturnNull_WhenThereIsNoAddress(string? geocoding)
    {
        // Act
        var address = GeocodedAddress.Get(geocoding);

        // Assert
        Assert.Null(address);
    }

    private static string Features(params string[] features)
    {
        return $"{{\"type\":\"FeatureCollection\",\"features\":[{string.Join(",", features)}]}}";
    }

    private static string Feature(string featureType, string? fullAddress = null, string? name = null,
        string? placeFormatted = null)
    {
        var properties = new Dictionary<string, string?>
        {
            ["feature_type"] = featureType,
            ["full_address"] = fullAddress,
            ["name"] = name,
            ["place_formatted"] = placeFormatted
        };

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "Feature",
            properties = properties.Where(a => a.Value != null).ToDictionary(a => a.Key, a => a.Value)
        });
    }
}
