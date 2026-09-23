using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Tests;

public class PhotoSourceDataServiceTests
{
    private const long UserId = 1;
    private const long SourceId = 1;

    private readonly Mock<IPhotoService> _photoService = new();
    private readonly Mock<IUserPhotoSourceService> _userPhotoSourceService = new();
    private readonly Mock<IPhotoSourceProcessingService> _processingService = new();
    private readonly Mock<IFileStorage> _fileStorage = new();
    private readonly Mock<IFrontendNotificationService> _frontendNotificationService = new();

    public PhotoSourceDataServiceTests()
    {
        _photoService
            .Setup(a => a.DeleteByPhotoSourceAsync(UserId, SourceId))
            .ReturnsAsync(["Dropbox/1/thumbs/photo_256.jpg", "Dropbox/1/thumbs/photo_640.jpg"]);
    }

    [Fact]
    public async Task DeleteDataAsync_ShouldDeleteThePhotosThumbnailsStatusAndState()
    {
        // Arrange
        var service = CreateService();

        // Act
        var deleted = await service.DeleteDataAsync(UserId, SourceId);

        // Assert
        Assert.True(deleted);

        _photoService.Verify(a => a.DeleteByPhotoSourceAsync(UserId, SourceId));
        _fileStorage.Verify(a => a.Delete("Dropbox/1/thumbs/photo_256.jpg"));
        _fileStorage.Verify(a => a.Delete("Dropbox/1/thumbs/photo_640.jpg"));
        _userPhotoSourceService.Verify(a => a.DeleteUserPhotoStatusAsync(UserId, SourceId));

        // the source resumes from its state, which has to go with the photos it was downloading
        _userPhotoSourceService.Verify(a => a.UpdateUserPhotoStateAsync(UserId, SourceId, null));
    }

    [Fact]
    public async Task DeleteDataAsync_ShouldReportTheSourceAsNotStarted()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.DeleteDataAsync(UserId, SourceId);

        // Assert
        _frontendNotificationService.Verify(a => a.SendProgressAsync(It.Is<UserPhotoSourceStatus>(b =>
            b.PhotoSourceId == SourceId && b.Status == PhotoSourceStatus.NotStarted && b.ProcessedCount == 0 &&
            b.TotalCount == 0)));
    }

    [Fact]
    public async Task DeleteDataAsync_ShouldDeleteNothing_WhileTheSourceIsBeingProcessed()
    {
        // Arrange
        _processingService.Setup(a => a.IsRunning(UserId, SourceId)).Returns(true);

        var service = CreateService();

        // Act
        var deleted = await service.DeleteDataAsync(UserId, SourceId);

        // Assert
        Assert.False(deleted);

        _photoService.VerifyNoOtherCalls();
        _userPhotoSourceService.VerifyNoOtherCalls();
        _fileStorage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteDataAsync_ShouldDeleteTheRest_WhenAThumbnailCannotBeDeleted()
    {
        // Arrange
        _fileStorage.Setup(a => a.Delete("Dropbox/1/thumbs/photo_256.jpg")).Throws<IOException>();

        var service = CreateService();

        // Act
        var deleted = await service.DeleteDataAsync(UserId, SourceId);

        // Assert: the photo is gone from the database either way
        Assert.True(deleted);

        _fileStorage.Verify(a => a.Delete("Dropbox/1/thumbs/photo_640.jpg"));
        _userPhotoSourceService.Verify(a => a.DeleteUserPhotoStatusAsync(UserId, SourceId));
    }

    private PhotoSourceDataService CreateService()
    {
        return new PhotoSourceDataService(_photoService.Object, _userPhotoSourceService.Object,
            _processingService.Object, _fileStorage.Object, _frontendNotificationService.Object,
            NullLogger<PhotoSourceDataService>.Instance);
    }
}
