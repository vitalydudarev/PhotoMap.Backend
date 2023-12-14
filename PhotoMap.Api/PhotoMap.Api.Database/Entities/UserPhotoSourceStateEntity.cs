namespace PhotoMap.Api.Database.Entities;

public class UserPhotoSourceStateEntity
{
    public required long UserId { get; set; }
    public UserEntity? User { get; set; }
    public required long PhotoSourceId { get; set; }
    public PhotoSourceEntity? PhotoSource { get; set; }
    public string? State { get; set; }
}