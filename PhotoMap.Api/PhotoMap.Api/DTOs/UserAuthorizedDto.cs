namespace PhotoMap.Api.DTOs;

public class UserAuthorizedDto
{
    public required long UserId { get; set; }
    public required string Token { get; set; }
    public int? TokenExpiresIn { get; set; }
}