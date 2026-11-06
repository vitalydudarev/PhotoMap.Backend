namespace PhotoMap.Api.Database.Entities;

public class PhotoTagEntity
{
    public long PhotoId { get; set; }
    public PhotoEntity? Photo { get; set; }
    public long TagId { get; set; }
    public TagEntity? Tag { get; set; }
}
