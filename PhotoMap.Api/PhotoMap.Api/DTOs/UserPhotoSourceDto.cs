namespace PhotoMap.Api.DTOs;

public class UserPhotoSourceDto
{
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    public string PhotoSourceName { get; set; } = null!;
    public bool IsUserAuthorized { get; set; }
    public string? TokenExpiresOn { get; set; }
    public UserPhotoSourceStatusDto Status { get; set; }
}