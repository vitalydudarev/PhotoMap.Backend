namespace PhotoMap.Api.Database.Entities;

public class TagEntity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public UserEntity? User { get; set; }

    /// <summary>
    /// The name of the tag, as the user first wrote it.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The name in lower case, which tells the tags of a user apart: Paris and paris are the same tag.
    /// </summary>
    public required string NormalizedName { get; set; }

    public List<PhotoTagEntity> Photos { get; set; } = [];
}
