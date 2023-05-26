namespace PhotoMap.Api.Domain.Models;

public class AuthSettings
{
    public required OAuthConfiguration OAuthConfiguration { get; set; }
    public required string RelativeAuthUrl { get; set; }
}