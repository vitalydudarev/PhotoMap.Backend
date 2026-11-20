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

    /// <summary>
    /// How far off the GPS location may be, in meters, from the GPSHPositioningError tag of the EXIF; null when the
    /// photo has none.
    /// </summary>
    public double? HorizontalPositioningError { get; set; }
    public bool HasGps { get; set; }
    public string? ExifString { get; set; }
    public string? Path { get; set; }

    /// <summary>
    /// The folder of the photo in the photo source, without its file name, such as <c>disk:/Camera Uploads</c>, and
    /// <c>disk:/</c> for a photo at the root of the disk; null when the photo has no path. The database works it out
    /// from <see cref="Path"/>, as the folder of a video is, see <c>VideoProcessingService.GetFolderPath</c>.
    /// </summary>
    public string? FolderPath { get; set; }
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
    /// The photo is outside the folder the source is processed from.
    /// </summary>
    public bool FromOtherUploads { get; set; }

    /// <summary>
    /// The version of the rules the photo was put in its categories by, 0 until it has been.
    /// </summary>
    public int CategoriesVersion { get; set; }

    /// <summary>
    /// The address of the GPS location, the response of the Mapbox reverse geocoding as is (a GeoJSON
    /// FeatureCollection); null until the photo has been geocoded, and for the photos without GPS.
    /// </summary>
    public string? Geocoding { get; set; }
    public List<PhotoCategoryEntity> Categories { get; set; } = [];
    public List<PhotoTagEntity> Tags { get; set; } = [];

    /// <summary>
    /// The locations the photo was taken in, read from <see cref="Geocoding"/>.
    /// </summary>
    public List<PhotoLocationEntity> Locations { get; set; } = [];
}