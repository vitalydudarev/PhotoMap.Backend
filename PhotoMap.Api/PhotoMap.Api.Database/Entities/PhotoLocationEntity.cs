namespace PhotoMap.Api.Database.Entities;

/// <summary>
/// A location a photo was taken in, one of each level at most.
/// </summary>
public class PhotoLocationEntity
{
    public long PhotoId { get; set; }
    public PhotoEntity? Photo { get; set; }
    public long LocationId { get; set; }
    public LocationEntity? Location { get; set; }
}
