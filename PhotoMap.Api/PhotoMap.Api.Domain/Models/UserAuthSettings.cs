namespace PhotoMap.Api.Domain.Models;

public class UserAuthSettings
{
    public required string Token { get; set; }
    public required DateTimeOffset TokenExpiresOn { get; set; }
}