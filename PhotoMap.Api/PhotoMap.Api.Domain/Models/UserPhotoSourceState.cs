namespace PhotoMap.Api.Domain.Models;

public class UserPhotoSourceState
{
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    public string? State { get; set; }
}