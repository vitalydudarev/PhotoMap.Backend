using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class PhotoCategorizerTests
{
    [Theory]
    [InlineData("Screenshot 2024-05-01.png")]
    [InlineData("IMG_0001.PNG")]
    public void Categorize_ShouldPutPngFilesInScreenshots(string fileName)
    {
        Assert.Equal([PhotoCategory.Screenshot], PhotoCategorizer.Categorize(fileName, null));
    }

    [Theory]
    [InlineData("""{"ExifIfd0":{"Make":"DJI","Model":"FC3582"}}""")]
    [InlineData("""{"ExifIfd0":{"Make":"dji "}}""")]
    [InlineData("""{"ExifSubIfd":{"Width":4000,"Height":3000},"ExifIfd0":{"Make":"Apple"}}""")]
    public void Categorize_ShouldPutPhotosOfADjiCameraOrOf4000By3000InDroneFootage(string exif)
    {
        Assert.Equal([PhotoCategory.DroneFootage], PhotoCategorizer.Categorize("DJI_0001.JPG", exif));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("""{"ExifIfd0":null,"ExifSubIfd":null,"Gps":null}""")]
    [InlineData("""{"ExifIfd0":{"Make":"Apple"},"ExifSubIfd":{"Width":4032,"Height":3024}}""")]
    [InlineData("""{"ExifSubIfd":{"Width":3000,"Height":4000}}""")]
    [InlineData("""{"ExifSubIfd":{"Width":4000,"Height":null}}""")]
    public void Categorize_ShouldPutOtherPhotosInNoCategory(string? exif)
    {
        Assert.Empty(PhotoCategorizer.Categorize("IMG_0001.jpg", exif));
    }

    [Fact]
    public void Categorize_ShouldPutAPhotoInEveryCategoryItIsIn()
    {
        Assert.Equal([PhotoCategory.Screenshot, PhotoCategory.DroneFootage],
            PhotoCategorizer.Categorize("frame.png", """{"ExifIfd0":{"Make":"DJI"}}"""));
    }
}
