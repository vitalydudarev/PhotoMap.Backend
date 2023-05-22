namespace PhotoMap.Api.Domain.Models;

public class UserPhotoSourceSettings
{
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    public string PhotoSourceName { get; set; } = null!;
    public AuthResult? AuthSettings { get; set; }
    public bool IsUserAuthorized { get; set; }
}