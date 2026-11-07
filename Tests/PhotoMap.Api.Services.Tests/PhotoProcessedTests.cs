using System.Text.Json;
using PhotoMap.Shared.Messages;
using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Tests;

public class PhotoProcessedTests
{
    [Fact]
    public void Json_ShouldKeepAProcessedImage_AsKafkaCarriesIt()
    {
        // Arrange
        var message = new PhotoProcessed
        {
            RequestId = Guid.NewGuid(),
            UserId = 1,
            PhotoSourceId = 2,
            FileName = "a.jpg",
            Image = new ProcessedImage
            {
                FileName = "a.jpg",
                UserId = 1,
                PhotoSourceId = 2,
                PhotoSourceName = "Dropbox",
                Path = "/a.jpg",
                ExternalId = "id:a",
                ContentHash = "hash",
                FileCreatedOn = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                PhotoTakenOn = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
                Latitude = 52.1,
                Longitude = 21.2,
                ThumbnailFiles = new Dictionary<int, string> { [256] = "/bin/a.jpg.256.jpg" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(message);
        var received = JsonSerializer.Deserialize<PhotoProcessed>(json)!;

        // Assert
        Assert.Equal(message.RequestId, received.RequestId);
        Assert.True(received.Succeeded);
        Assert.Equal("id:a", received.Image!.ExternalId);
        Assert.Equal("/bin/a.jpg.256.jpg", received.Image.ThumbnailFiles[256]);
        Assert.Equal(52.1, received.Image.Latitude);
        // the kinds of the dates matter to how they are made UTC when the photo is saved
        Assert.Equal(DateTimeKind.Utc, received.Image.FileCreatedOn!.Value.Kind);
        Assert.Equal(DateTimeKind.Unspecified, received.Image.PhotoTakenOn!.Value.Kind);
        Assert.DoesNotContain(nameof(PhotoProcessed.Succeeded), json);
    }

    [Fact]
    public void Json_ShouldKeepAProcessImageRequest_AsKafkaCarriesIt()
    {
        // Arrange: DownloadedFileInfo has no parameterless constructor, the serializer goes through the other one
        var request = new ProcessImageRequest
        {
            RequestId = Guid.NewGuid(),
            DownloadedFileInfo = new DownloadedFileInfo("a.jpg", "disk:/a.jpg",
                new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc), "id:a") { FromOtherUploads = true },
            FileName = "/storage/Bin/1/2/abc-a.jpg",
            Sizes = [640, 256],
            UserId = 1,
            PhotoSourceId = 2,
            PhotoSourceName = "Yandex.Disk"
        };

        // Act
        var received = JsonSerializer.Deserialize<ProcessImageRequest>(JsonSerializer.Serialize(request))!;

        // Assert
        Assert.Equal(request.RequestId, received.RequestId);
        Assert.Equal("a.jpg", received.DownloadedFileInfo.ResourceName);
        Assert.Equal("disk:/a.jpg", received.DownloadedFileInfo.Path);
        Assert.Equal("id:a", received.DownloadedFileInfo.FileId);
        Assert.Equal(DateTimeKind.Utc, received.DownloadedFileInfo.CreatedOn!.Value.Kind);
        Assert.True(received.DownloadedFileInfo.FromOtherUploads);
        Assert.Equal("/storage/Bin/1/2/abc-a.jpg", received.FileName);
        Assert.Equal([640, 256], received.Sizes);
        Assert.Equal("Yandex.Disk", received.PhotoSourceName);
        Assert.Equal(request.RequestId.ToString("N"), received.GetKey());
    }

    [Fact]
    public void Json_ShouldKeepAFailure()
    {
        // Arrange
        var message = new PhotoProcessed
        {
            RequestId = Guid.NewGuid(),
            UserId = 1,
            PhotoSourceId = 2,
            FileName = "a.jpg",
            Error = "Failed to process the image: empty"
        };

        // Act
        var received = JsonSerializer.Deserialize<PhotoProcessed>(JsonSerializer.Serialize(message))!;

        // Assert
        Assert.False(received.Succeeded);
        Assert.Null(received.Image);
        Assert.Equal("Failed to process the image: empty", received.Error);
        Assert.Equal("1:2", received.GetKey());
    }
}
