using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Entities;

public class UserPhotoSourceEntity
{
    public long Id { get; set; }
    public required long UserId { get; set; }
    public UserEntity? User { get; set; }
    public required long PhotoSourceId { get; set; }
    public PhotoSourceEntity? PhotoSource { get; set; }
    public AuthResult? AuthSettings { get; set; }
}