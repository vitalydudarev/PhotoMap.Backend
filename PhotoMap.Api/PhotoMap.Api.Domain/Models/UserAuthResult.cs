namespace PhotoMap.Api.Domain.Models;

public class UserAuthResult
{
    public required string Token { get; set; }
    public required DateTimeOffset TokenExpiresOn { get; set; }
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Access tokens are short-lived, with a refresh token a new one is obtained whenever it expires.
    /// </summary>
    public bool IsValid => RefreshToken != null || TokenExpiresOn > DateTimeOffset.UtcNow;
}
