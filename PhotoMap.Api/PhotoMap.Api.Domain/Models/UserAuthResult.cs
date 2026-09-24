namespace PhotoMap.Api.Domain.Models;

public class UserAuthResult
{
    public required string Token { get; set; }
    public required DateTimeOffset TokenExpiresOn { get; set; }
    public string? RefreshToken { get; set; }

    /// <summary>
    /// The user is authorized while the access token has not expired. A refresh token does not extend that: the
    /// tokens it obtains during a run are not saved, so once the access token has expired the user authorizes again.
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(Token) && TokenExpiresOn > DateTimeOffset.UtcNow;
}
