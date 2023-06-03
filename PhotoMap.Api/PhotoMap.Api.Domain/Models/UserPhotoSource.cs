namespace PhotoMap.Api.Domain.Models;

public class UserPhotoSource
{
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    public string PhotoSourceName { get; set; } = null!;
    public bool IsUserAuthorized { get; set; }
    public DateTime? TokenExpiresOn { get; set; }
}