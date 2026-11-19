namespace PhotoMap.Api.Database.Entities;

/// <summary>
/// A location a video was taken in, one of each level at most.
/// </summary>
public class VideoLocationEntity
{
    public long VideoId { get; set; }
    public VideoEntity? Video { get; set; }
    public long LocationId { get; set; }
    public LocationEntity? Location { get; set; }
}
