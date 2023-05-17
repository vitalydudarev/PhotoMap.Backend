namespace PhotoMap.Api.Domain.Models;

public class AuthResult
{
    public required string Token { get; set; }
    public required DateTimeOffset TokenExpiresOn { get; set; }
}