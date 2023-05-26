namespace PhotoMap.Api.DTOs;

public class UserPhotoSourceDto
{
    public long UserId { get; set; }
    public long PhotoSourceId { get; set; }
    public string PhotoSourceName { get; set; } = null!;
    public AuthResultOutputDto? AuthResult { get; set; }
    public bool IsUserAuthorized { get; set; }
}