using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Tests;

public class PhotoSourceDataServiceTests
{
    private const long UserId = 1;
    private const long SourceId = 1;
    private const long OtherSourceId = 2;

    private readonly Mock<IPhotoService> _photoService = new();
    private readonly Mock<IUserPhotoSourceService> _userPhotoSourceService = new();
    private readonly Mock<IFailedFileService> _failedFileService = new();
    private readonly Mock<IPhotoSourceProcessingService> _processingService = new();
    private readonly Mock<IFileStorage> _fileStorage = new();
    private readonly Mock<IFrontendNotificationService> _frontendNotificationService = new();

    public PhotoSourceDataServiceTests()
    {
        _photoService
            .Setup(a => a.DeleteByPhotoSourceAsync(UserId, SourceId))
            .ReturnsAsync(["Dropbox/1/thumbs/photo_256.jpg", "Dropbox/1/thumbs/photo_640.jpg"]);
        _photoService
            .Setup(a => a.DeleteByPhotoSourceAsync(UserId, OtherSourceId))
            .ReturnsAsync(["Yandex.Disk/1/thumbs/photo_256.jpg"]);
    }

    [Fact]
    public async Task DeleteDataAsync_ShouldDeleteThePhotosThumbnailsStatusStateAndFailedFiles()
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

        // the files that failed are downloaded again by the next run, with the rest of the source
        _failedFileService.Verify(a => a.DeleteByPhotoSourceAsync(UserId, SourceId));
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
        _failedFileService.VerifyNoOtherCalls();
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

    [Fact]
    public async Task DeleteAllDataAsync_ShouldDeleteTheDataOfEverySource()
    {
        // Arrange
        _userPhotoSourceService
            .Setup(a => a.GetAllUserPhotoSourceIdsAsync())
            .ReturnsAsync([(UserId, SourceId), (UserId, OtherSourceId)]);

        var service = CreateService();

        // Act
        var deleted = await service.DeleteAllDataAsync();

        // Assert
        Assert.True(deleted);

        foreach (var sourceId in new[] { SourceId, OtherSourceId })
        {
            _photoService.Verify(a => a.DeleteByPhotoSourceAsync(UserId, sourceId));
            _userPhotoSourceService.Verify(a => a.DeleteUserPhotoStatusAsync(UserId, sourceId));
            _userPhotoSourceService.Verify(a => a.UpdateUserPhotoStateAsync(UserId, sourceId, null));
            _failedFileService.Verify(a => a.DeleteByPhotoSourceAsync(UserId, sourceId));
        }

        _fileStorage.Verify(a => a.Delete("Dropbox/1/thumbs/photo_256.jpg"));
        _fileStorage.Verify(a => a.Delete("Yandex.Disk/1/thumbs/photo_256.jpg"));
    }

    [Fact]
    public async Task DeleteAllDataAsync_ShouldDeleteNothing_WhileAnySourceIsBeingProcessed()
    {
        // Arrange
        _userPhotoSourceService
            .Setup(a => a.GetAllUserPhotoSourceIdsAsync())
            .ReturnsAsync([(UserId, SourceId), (UserId, OtherSourceId)]);
        _processingService.Setup(a => a.IsRunning(UserId, OtherSourceId)).Returns(true);

        var service = CreateService();

        // Act
        var deleted = await service.DeleteAllDataAsync();

        // Assert: not even the source listed before the running one
        Assert.False(deleted);

        _photoService.VerifyNoOtherCalls();
        _failedFileService.VerifyNoOtherCalls();
        _fileStorage.VerifyNoOtherCalls();
    }

    private PhotoSourceDataService CreateService()
    {
        return new PhotoSourceDataService(_photoService.Object, _userPhotoSourceService.Object,
            _failedFileService.Object, _processingService.Object, _fileStorage.Object, _frontendNotificationService.Object,
            NullLogger<PhotoSourceDataService>.Instance);
    }
}
