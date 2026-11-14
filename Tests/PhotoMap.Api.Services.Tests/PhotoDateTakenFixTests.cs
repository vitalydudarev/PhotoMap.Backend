using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Services.Services;
using PhotoMap.Worker.Helpers;
using PhotoMap.Worker.Models.Image;

namespace PhotoMap.Api.Services.Tests;

public class PhotoDateTakenFixTests
{
    private const string InstagramExif =
        """{"ExifSubIfd":{"DateTimeOriginal":"2023-05-01T12:34:56+03:00"},"Gps":{"DateTimeStamp":"1970-01-01T00:00:00Z"}}""";

    private readonly Mock<IPhotoRepository> _photoRepository = new();

    [Fact]
    public void GetDateTaken_ShouldReadDateTimeOriginal_WhenTheGpsTimeIsTheUnixEpoch()
    {
        Assert.Equal(new DateTimeOffset(2023, 5, 1, 9, 34, 56, TimeSpan.Zero), PhotoDateTakenFixer.GetDateTaken(InstagramExif));
    }

    [Theory]
    [InlineData("""{"ExifSubIfd":{"DateTimeOriginal":"2023-05-01T12:34:56+03:00"},"Gps":{"DateTimeStamp":"2023-05-01T09:34:50Z"}}""")]
    [InlineData("""{"ExifSubIfd":{"DateTimeOriginal":"2023-05-01T12:34:56+03:00"},"Gps":null}""")]
    [InlineData("""{"ExifSubIfd":{"DateTimeOriginal":null},"Gps":{"DateTimeStamp":"1970-01-01T00:00:00Z"}}""")]
    [InlineData("not json")]
    [InlineData(null)]
    public void GetDateTaken_ShouldReturnNull_WhenTheExifTellsNothingBetter(string? exifString)
    {
        Assert.Null(PhotoDateTakenFixer.GetDateTaken(exifString));
    }

    [Fact]
    public async Task FixNextAsync_ShouldSetWhenThePhotosWereTaken_AndSkipTheOnesItCannotTell()
    {
        // Arrange
        _photoRepository
            .Setup(a => a.GetTakenAtAsync(PhotoDateTakenFixer.NoGpsTime, 0, 10))
            .ReturnsAsync([CreatePhoto(3, InstagramExif), CreatePhoto(7, null)]);

        IReadOnlyDictionary<long, DateTimeOffset>? saved = null;
        _photoRepository
            .Setup(a => a.SetDatesTakenAsync(It.IsAny<IReadOnlyDictionary<long, DateTimeOffset>>()))
            .Callback<IReadOnlyDictionary<long, DateTimeOffset>>(a => saved = a)
            .Returns(Task.CompletedTask);

        var yearsCache = new PhotoYearsCache();
        await yearsCache.GetOrLoadAsync(1, () => Task.FromResult<IEnumerable<int>>([1970, 2023]));

        var service = new PhotoDateTakenFixService(_photoRepository.Object, yearsCache);

        // Act
        var (lastId, fixedCount) = await service.FixNextAsync(0, 10);

        // Assert
        Assert.Equal(7, lastId);
        Assert.Equal(1, fixedCount);
        Assert.NotNull(saved);
        var entry = Assert.Single(saved);
        Assert.Equal(3, entry.Key);
        Assert.Equal(new DateTimeOffset(2023, 5, 1, 9, 34, 56, TimeSpan.Zero), entry.Value);

        // the years are read again from the database
        Assert.Equal([2024], await yearsCache.GetOrLoadAsync(1, () => Task.FromResult<IEnumerable<int>>([2024])));
    }

    [Fact]
    public async Task FixNextAsync_ShouldReturnNoLastId_WhenNoPhotoIsLeft()
    {
        // Arrange
        _photoRepository.Setup(a => a.GetTakenAtAsync(PhotoDateTakenFixer.NoGpsTime, 5, 10)).ReturnsAsync([]);
        var service = new PhotoDateTakenFixService(_photoRepository.Object, new PhotoYearsCache());

        // Act
        var (lastId, fixedCount) = await service.FixNextAsync(5, 10);

        // Assert
        Assert.Null(lastId);
        Assert.Equal(0, fixedCount);
        _photoRepository.Verify(a => a.SetDatesTakenAsync(It.IsAny<IReadOnlyDictionary<long, DateTimeOffset>>()),
            Times.Never);
    }

    [Fact]
    public void ExifHelper_GetDate_ShouldSkipTheGpsTime_WhenItIsTheUnixEpoch()
    {
        var taken = new DateTime(2023, 5, 1, 9, 34, 56, DateTimeKind.Utc);
        var exif = new ExifData
        {
            Gps = new Gps { DateTimeStamp = DateTime.UnixEpoch },
            ExifSubIfd = new ExifSubIfd { DateTimeOriginal = taken }
        };

        Assert.Equal(taken, ExifHelper.GetDate(exif));
    }

    private static Photo CreatePhoto(long id, string? exifString)
    {
        return new Photo
        {
            Id = id,
            UserId = 1,
            PhotoSourceId = 1,
            FileName = $"{id}.jpg",
            ExifString = exifString,
            DateTimeTaken = PhotoDateTakenFixer.NoGpsTime,
            AddedOn = DateTimeOffset.UtcNow
        };
    }
}
