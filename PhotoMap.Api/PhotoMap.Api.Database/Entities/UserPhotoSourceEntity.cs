using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Entities;

public class UserPhotoSourceEntity
{
    public required long UserId { get; set; }
    public UserEntity? User { get; set; }
    public required long PhotoSourceId { get; set; }
    public PhotoSourceEntity? PhotoSource { get; set; }
    public UserAuthResult? UserAuthResult { get; set; }
    public string? ProcessingState { get; set; }
}