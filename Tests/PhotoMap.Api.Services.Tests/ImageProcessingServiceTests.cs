using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Shared.Models;
using PhotoMap.Worker.Services.Definitions;
using PhotoMap.Worker.Services.Implementations;
using SkiaSharp;

namespace PhotoMap.Api.Services.Tests;

public class ImageProcessingServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public ImageProcessingServiceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void ProcessImage_ShouldMarkThePhoto_WhenItIsFromAnotherFolder()
    {
        // Arrange
        var service = new ImageProcessingService(NullLogger<ImageProcessingService>.Instance, new Mock<IExifExtractor>().Object);
        var request = CreateRequest(new DownloadedFileInfo("c.jpg", "disk:/Photos/c.jpg", DateTime.UtcNow, "id")
        {
            FromOtherFolder = true
        });

        // Act
        var processedImage = service.ProcessImage(request);

        // Assert
        Assert.True(processedImage.FromOtherFolder);
        Assert.Equal([256], processedImage.Thumbs.Keys);
    }

    [Fact]
    public void ProcessImage_ShouldHashTheFile()
    {
        // Arrange
        var service = new ImageProcessingService(NullLogger<ImageProcessingService>.Instance, new Mock<IExifExtractor>().Object);
        var request = CreateRequest(new DownloadedFileInfo("a.png", "/a.png", DateTime.UtcNow, "id"));

        // Act
        var processedImage = service.ProcessImage(request);

        // Assert
        Assert.Equal(64, processedImage.ContentHash.Length);
        Assert.Null(processedImage.Latitude);
        Assert.False(processedImage.FromOtherFolder);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private ProcessImageRequest CreateRequest(DownloadedFileInfo fileInfo)
    {
        using var bitmap = new SKBitmap(32, 24);
        bitmap.Erase(SKColors.CornflowerBlue);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        var filePath = Path.Combine(_directory, Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(filePath, data.ToArray());

        return new ProcessImageRequest
        {
            DownloadedFileInfo = fileInfo,
            FileName = filePath,
            Sizes = [256],
            UserId = 1,
            PhotoSourceId = 2,
            PhotoSourceName = "Yandex.Disk"
        };
    }
}
