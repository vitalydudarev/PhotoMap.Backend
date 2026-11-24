namespace PhotoMap.Api.Database.Entities;

/// <summary>
/// A tag of a video; the tags are those of the photos too, so that a tag means the same for both.
/// </summary>
public class VideoTagEntity
{
    public long VideoId { get; set; }
    public VideoEntity? Video { get; set; }
    public long TagId { get; set; }
    public TagEntity? Tag { get; set; }
}
