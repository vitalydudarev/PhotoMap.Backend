namespace PhotoMap.Api.DTOs;

public class UserPhotoSourceSettingsDto
{
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    public string PhotoSourceName { get; set; } = null!;
    public AuthResultDto? AuthResult { get; set; }
    public bool IsUserAuthorized { get; set; }
}