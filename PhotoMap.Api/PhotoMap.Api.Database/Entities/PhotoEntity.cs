namespace PhotoMap.Api.Database.Entities;

public class PhotoEntity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public UserEntity? User { get; set; }
    public long PhotoSourceId { get; set; }
    public PhotoSourceEntity? PhotoSource { get; set; }
    public string? ThumbnailSmallFilePath { get; set; }
    public string? ThumbnailLargeFilePath { get; set; }
    public required string FileName { get; set; }
    public required DateTimeOffset DateTimeTaken { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool HasGps { get; set; }
    public string? ExifString { get; set; }
    public string? Path { get; set; }
    public string? ExternalId { get; set; }
    public string? ContentHash { get; set; }
    public required DateTimeOffset AddedOn { get; set; }

    /// <summary>
    /// When the user marked the photo as deleted, null while it is not.
    /// </summary>
    public DateTimeOffset? DeletedOn { get; set; }

    /// <summary>
    /// The group of the photos of the user this one is a copy of, the ID of the first of them; null when it has none.
    /// </summary>
    public long? DuplicateGroupId { get; set; }

    /// <summary>
    /// The version of the rules the photo was put in its categories by, 0 until it has been.
    /// </summary>
    public int CategoriesVersion { get; set; }
    public List<PhotoCategoryEntity> Categories { get; set; } = [];
}