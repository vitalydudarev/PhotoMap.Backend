using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services.Tests;

public class GeoBoxTests
{
    [Fact]
    public void TryParse_ShouldReadTheSidesInTheOrderOfAGeoJsonBoundingBox()
    {
        // Act
        var parsed = GeoBox.TryParse("-9.23,38.69,-9.09,38.8", out var box);

        // Assert
        Assert.True(parsed);
        Assert.Equal(new GeoBox(38.69, 38.8, -9.23, -9.09), box);
    }

    [Fact]
    public void TryParse_ShouldTakeTheWholeWorld()
    {
        // Act
        var parsed = GeoBox.TryParse("-180,-90,180,90", out var box);

        // Assert
        Assert.True(parsed);
        Assert.Equal(new GeoBox(-90, 90, -180, 180), box);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1,2,3")]
    [InlineData("1,2,3,4,5")]
    [InlineData("1,2,3,x")]
    [InlineData("1;2;3;4")]
    [InlineData("1,2,NaN,4")]
    [InlineData("1,2,Infinity,4")]
    [InlineData("-181,0,0,1")]
    [InlineData("0,0,181,1")]
    [InlineData("0,-91,1,0")]
    [InlineData("0,0,1,91")]
    [InlineData("2,0,1,1")]
    [InlineData("0,2,1,1")]
    public void TryParse_ShouldNotReadText_ThatIsNotABox(string? text)
    {
        // Act
        var parsed = GeoBox.TryParse(text, out var box);

        // Assert
        Assert.False(parsed);
        Assert.Null(box);
    }
}
