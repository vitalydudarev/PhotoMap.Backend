namespace PhotoMap.Api.DTOs;

public class AuthResultOutputDto
{
    public required string Token { get; set; }
    public required string TokenExpiresOn { get; set; }
}